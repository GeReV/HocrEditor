using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using Microsoft.Extensions.ObjectPool;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using Rect = HocrEditor.Models.Rect;
using Size = System.Windows.Size;

namespace HocrEditor.Controls;

internal enum MouseState
{
    None,
    Panning,
    Dragging,
    Resizing,
    SelectingNodes,
    SelectingRegion,
    DraggingSelectionRegion,
    DraggingWordSplitter,
}

public class Element
{
    public SKRect Bounds { get; set; }
}

public sealed partial class DocumentCanvas
{
    private static readonly SKSize CenterPadding = new(-10.0f, -10.0f);

    private static readonly SKColor HighlightColor = new(0xffffff99);
    private static readonly SKColor NodeSelectorColor = new(0xff1f78b4);
    private static readonly SKColor NodeSelectionColor = new(0xffa6cee3);

    private static SKColor GetNodeColor(HocrNodeViewModel node) => node switch
    {
        // Node colors based on the D3 color category "Paired": https://observablehq.com/@d3/color-schemes
        // ["#a6cee3","#1f78b4","#b2df8a","#33a02c","#fb9a99","#e31a1c","#fdbf6f","#ff7f00","#cab2d6","#6a3d9a","#ffff99","#b15928"]
        _ when node.NodeType is HocrNodeType.Page => SKColor.Empty,
        _ when node.NodeType is HocrNodeType.ContentArea => new SKColor(0xffa6cee3),
        _ when node.NodeType is HocrNodeType.Paragraph => new SKColor(0xffb2df8a),
        _ when node.NodeType is HocrNodeType.Word => new SKColor(0xfffb9a99),
        _ when node.NodeType is HocrNodeType.Image => new SKColor(0xffcab2d6),
        _ when node.IsLineElement => new SKColor(0xfffdbf6f),
        _ => throw new ArgumentOutOfRangeException()
    };

    private readonly ObjectPool<Element> elementPool =
        new DefaultObjectPool<Element>(new DefaultPooledObjectPolicy<Element>());

    public static readonly DependencyProperty ViewModelProperty
        = DependencyProperty.Register(
            nameof(ViewModel),
            typeof(HocrPageViewModel),
            typeof(DocumentCanvas),
            new PropertyMetadata(
                null,
                ViewModelChanged
            )
        );

    public static readonly DependencyProperty NodeVisibilityProperty
        = DependencyProperty.Register(
            nameof(NodeVisibility),
            typeof(ReadOnlyObservableCollection<NodeVisibility>),
            typeof(DocumentCanvas),
            new PropertyMetadata(
                null,
                NodeVisibilityChanged
            )
        );

    public static readonly DependencyProperty IsShowThresholdedImageProperty = DependencyProperty.Register(
        nameof(IsShowThresholdedImage),
        typeof(bool),
        typeof(DocumentCanvas),
        new PropertyMetadata(
            false,
            IsShowThresholdedImageChanged
        )
    );

    public static readonly DependencyProperty IsShowTextProperty = DependencyProperty.Register(
        nameof(IsShowText),
        typeof(bool),
        typeof(DocumentCanvas),
        new PropertyMetadata(
            false,
            IsShowInfoChanged
        )
    );

    public static readonly DependencyProperty IsShowNumberingProperty = DependencyProperty.Register(
        nameof(IsShowNumbering),
        typeof(bool),
        typeof(DocumentCanvas),
        new PropertyMetadata(
            false,
            IsShowInfoChanged
        )
    );

    public static readonly DependencyProperty ActiveToolProperty = DependencyProperty.Register(
        nameof(ActiveTool),
        typeof(DocumentCanvasTool),
        typeof(DocumentCanvas),
        new FrameworkPropertyMetadata(
            DocumentCanvasTool.None,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            ActiveToolChanged
        )
    );

    public static readonly DependencyProperty SelectionBoundsProperty = DependencyProperty.Register(
        nameof(SelectionBounds),
        typeof(Rect),
        typeof(DocumentCanvas),
        new FrameworkPropertyMetadata(
            default(Rect),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            SelectionBoundsChanged
        )
    );

    public static readonly RoutedEvent NodesChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(NodesChanged),
        RoutingStrategy.Bubble,
        typeof(EventHandler<NodesChangedEventArgs>),
        typeof(DocumentCanvas)
    );

    public static readonly RoutedEvent NodesEditedEvent = EventManager.RegisterRoutedEvent(
        nameof(NodesEdited),
        RoutingStrategy.Bubble,
        typeof(EventHandler<NodesEditedEventArgs>),
        typeof(DocumentCanvas)
    );

    public static readonly RoutedEvent WordSplitEvent = EventManager.RegisterRoutedEvent(
        nameof(WordSplit),
        RoutingStrategy.Bubble,
        typeof(EventHandler<WordSplitEventArgs>),
        typeof(DocumentCanvas)
    );

    public ObservableHashSet<HocrNodeViewModel>? SelectedItems
    {
        get => ViewModel?.SelectedNodes;
        set
        {
            ArgumentNullException.ThrowIfNull(ViewModel);
            ArgumentNullException.ThrowIfNull(value);

            ViewModel.SelectedNodes = value;
        }
    }

    public HocrPageViewModel? ViewModel
    {
        get => (HocrPageViewModel)GetValue(ViewModelProperty);
        set
        {
            if (value == null)
            {
                ClearValue(ViewModelProperty);
            }
            else
            {
                SetValue(ViewModelProperty, value);
            }
        }
    }

    public ReadOnlyObservableCollection<NodeVisibility> NodeVisibility
    {
        get => (ReadOnlyObservableCollection<NodeVisibility>)GetValue(NodeVisibilityProperty);
        set => SetValue(NodeVisibilityProperty, value);
    }

    public bool IsShowThresholdedImage
    {
        get => (bool)GetValue(IsShowThresholdedImageProperty);
        set => SetValue(IsShowThresholdedImageProperty, value);
    }

    public bool IsShowText
    {
        get => (bool)GetValue(IsShowTextProperty);
        set => SetValue(IsShowTextProperty, value);
    }

    public bool IsShowNumbering
    {
        get => (bool)GetValue(IsShowNumberingProperty);
        set => SetValue(IsShowNumberingProperty, value);
    }

    public DocumentCanvasTool ActiveTool
    {
        get => (DocumentCanvasTool)GetValue(ActiveToolProperty);
        set => SetValue(ActiveToolProperty, value);
    }

    public Rect SelectionBounds
    {
        get => (Rect)GetValue(SelectionBoundsProperty);
        set => SetValue(SelectionBoundsProperty, value);
    }

    public event EventHandler<NodesChangedEventArgs> NodesChanged
    {
        add => AddHandler(NodesChangedEvent, value);
        remove => RemoveHandler(NodesChangedEvent, value);
    }

    public event EventHandler<NodesEditedEventArgs> NodesEdited
    {
        add => AddHandler(NodesEditedEvent, value);
        remove => RemoveHandler(NodesEditedEvent, value);
    }

    public event EventHandler<WordSplitEventArgs> WordSplit
    {
        add => AddHandler(NodesEditedEvent, value);
        remove => RemoveHandler(NodesEditedEvent, value);
    }

    public event SelectionChangedEventHandler? SelectionChanged;

    private int rootId = -1;
    private readonly Dictionary<int, (HocrNodeViewModel, Element)> elements = new();

    private CancellationTokenSource backgroundLoadCancellationTokenSource = new();
    private SKBitmapManager.SKBitmapReference? background;

    private Element RootElement => elements[rootId].Item2;

    private Dictionary<HocrNodeType, bool> nodeVisibilityDictionary = new();

    private readonly HashSet<int> selectedElements = new();

    private List<int> selectedKeyCandidates = new();
    private int selectedKey = -1;

    private HocrNodeViewModel? editingNode;

    private bool IsEditing => editingNode != null;

    private SKMatrix transformation = SKMatrix.Identity;
    private SKMatrix inverseTransformation = SKMatrix.Identity;
    private SKMatrix scaleTransformation = SKMatrix.Identity;
    private SKMatrix inverseScaleTransformation = SKMatrix.Identity;

    private MouseState mouseMoveState;

    // Debounce selection on mouse up by this duration to prevent a bounce
    // (double sequential selection, on mousedown then on mouseup immediately).
    private bool selectionChangedLatch;

    private SKPoint dragStart;
    private SKPoint offsetStart;

    private SKRect dragLimit = SKRect.Empty;

    private SKRect resizeLimitInside = SKRect.Empty;
    private SKRect resizeLimitOutside = SKRect.Empty;

    private SKRect nodeSelection = SKRect.Empty;

    private SKPoint wordSplitterPosition = SKPoint.Empty;
    private string wordSplitterValue = string.Empty;
    private int wordSplitterValueSplitStart;
    private int wordSplitterValueSplitLength;

    private Cursor? currentCursor;
    private readonly CanvasSelection canvasSelection = new();
    private ResizeHandle? selectedResizeHandle;

    private Window? parentWindow;

    private Window ParentWindow => parentWindow ??= Window.GetWindow(this) ?? throw new InvalidOperationException();

    public DocumentCanvas()
    {
        InitializeComponent();

        ClipToBounds = true;

        Unloaded += OnUnloaded;
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)sender;

        documentCanvas.canvasSelection.Dispose();

        documentCanvas.bidi.Dispose();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (!ReferenceEquals(e.OriginalSource, this))
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Return when ActiveTool == DocumentCanvasTool.WordSplitTool:
            {
                var first = wordSplitterValue;
                var second = first;

                if (wordSplitterValueSplitStart > 0 &&
                    wordSplitterValueSplitStart + wordSplitterValueSplitLength < wordSplitterValue.Length)
                {
                    var firstEnd = wordSplitterValueSplitStart;
                    first = wordSplitterValue[..firstEnd];

                    var secondStart = (wordSplitterValueSplitStart + wordSplitterValueSplitLength);
                    second = wordSplitterValue[secondStart..];
                }

                ArgumentNullException.ThrowIfNull(SelectedItems);

                var node = SelectedItems.First();

                Ensure.IsValid(nameof(node), node.NodeType == HocrNodeType.Word, "Expected node to be a word");

                var splitPosition = (int)wordSplitterPosition.X;

                // Reset tool, which will clear selection.
                ActiveTool = DocumentCanvasTool.None;

                // Split the word, which selects a node, so order with previous statement matters.
                OnWordSplit(node, splitPosition, (first, second));

                break;
            }
            case Key.Return:
            {
                BeginEditing();

                break;
            }
            case Key.Escape:
            {
                ActiveTool = DocumentCanvasTool.None;

                break;
            }
            case Key.Home:
            {
                e.Handled = true;

                CenterTransformationDocument();
                Refresh();

                break;
            }
            case Key.F:
            {
                if (!canvasSelection.ShouldShowCanvasSelection)
                {
                    break;
                }

                e.Handled = true;

                CenterTransformationSelection();

                Refresh();

                break;
            }
        }
    }

    private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        foreach (var (_, element) in documentCanvas.elements.Values)
        {
            documentCanvas.elementPool.Return(element);
        }

        documentCanvas.ClearCanvasSelection();

        documentCanvas.elements.Clear();

        documentCanvas.rootId = -1;

        documentCanvas.background = null;

        if (e.OldValue is HocrPageViewModel oldPage)
        {
            documentCanvas.backgroundLoadCancellationTokenSource.Cancel();

            oldPage.Nodes.UnsubscribeItemPropertyChanged(documentCanvas.NodesOnItemPropertyChanged);

            oldPage.Nodes.CollectionChanged -= documentCanvas.NodesOnCollectionChanged;

            oldPage.SelectedNodes.CollectionChanged -= documentCanvas.SelectedNodesOnCollectionChanged;
        }

        if (e.NewValue is HocrPageViewModel newPage)
        {
            documentCanvas.backgroundLoadCancellationTokenSource = new CancellationTokenSource();

            documentCanvas.background = documentCanvas.IsShowThresholdedImage ? newPage.ThresholdedImage : newPage.Image;

            newPage.Nodes.SubscribeItemPropertyChanged(documentCanvas.NodesOnItemPropertyChanged);

            newPage.Nodes.CollectionChanged += documentCanvas.NodesOnCollectionChanged;

            newPage.SelectedNodes.CollectionChanged += documentCanvas.SelectedNodesOnCollectionChanged;

            if (newPage.Nodes.Any())
            {
                documentCanvas.BuildDocumentElements(newPage.Nodes);

                documentCanvas.CenterTransformationDocument();
            }
        }

        documentCanvas.Refresh();
    }

    private void NodesOnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(sender);

        var node = (HocrNodeViewModel)sender;

        switch (e.PropertyName)
        {
            case nameof(HocrNodeViewModel.BBox):
                elements[node.Id].Item2.Bounds = node.BBox.ToSKRect();
                break;
            case nameof(HocrNodeViewModel.IsSelected):
                var enumerable = Enumerable.Repeat(node, 1);

                if (node.IsSelected)
                {
                    AddSelectedElements(enumerable);
                }
                else
                {
                    RemoveSelectedElements(enumerable);
                }

                break;
        }

        UpdateCanvasSelection();

        Refresh();
    }

    private static void NodeVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        if (e.OldValue is ReadOnlyObservableCollection<NodeVisibility> oldNodes && oldNodes.Any())
        {
            oldNodes.UnsubscribeItemPropertyChanged(documentCanvas.UpdateNodeVisibility);
        }

        if (e.NewValue is ReadOnlyObservableCollection<NodeVisibility> newNodes && newNodes.Any())
        {
            documentCanvas.nodeVisibilityDictionary = new Dictionary<HocrNodeType, bool>(
                newNodes.Select(nv => new KeyValuePair<HocrNodeType, bool>(nv.NodeTypeViewModel.NodeType, nv.Visible))
            );

            newNodes.SubscribeItemPropertyChanged(documentCanvas.UpdateNodeVisibility);

            documentCanvas.Refresh();
        }
    }

    private void UpdateNodeVisibility(object? sender, PropertyChangedEventArgs propertyChangedEventArgs)
    {
        if (sender is not NodeVisibility nodeVisibility)
        {
            throw new ArgumentException($"Expected {nameof(sender)} to be of type {nameof(NodeVisibility)}.");
        }

        nodeVisibilityDictionary[nodeVisibility.NodeTypeViewModel.NodeType] = nodeVisibility.Visible;

        Refresh();
    }

    private static void IsShowThresholdedImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        documentCanvas.background = (bool)e.NewValue ? documentCanvas.ViewModel?.ThresholdedImage : documentCanvas.ViewModel?.Image;

        documentCanvas.Refresh();
    }

    private static void IsShowInfoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        documentCanvas.Refresh();
    }

    private static void ActiveToolChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        var newValue = (DocumentCanvasTool)e.NewValue;

        switch (newValue)
        {
            case DocumentCanvasTool.None:
            {
                documentCanvas.Cursor = documentCanvas.currentCursor = null;

                documentCanvas.ClearCanvasSelection();

                documentCanvas.wordSplitterPosition = SKPoint.Empty;
                documentCanvas.wordSplitterValue = string.Empty;
                documentCanvas.wordSplitterValueSplitStart = 0;
                documentCanvas.wordSplitterValueSplitLength = 0;

                break;
            }
            case DocumentCanvasTool.SelectionTool:
            {
                documentCanvas.Cursor = documentCanvas.currentCursor = Cursors.Cross;

                // Transfer the current selection when we turn on the tool.
                if (!documentCanvas.canvasSelection.IsEmpty)
                {
                    documentCanvas.SelectionBounds = Rect.FromSKRect(documentCanvas.canvasSelection.Bounds);
                }

                break;
            }
            case DocumentCanvasTool.WordSplitTool:
            {
                documentCanvas.Cursor = documentCanvas.currentCursor = null;

                if (documentCanvas.IsEditing)
                {
                    documentCanvas.OnNodeEdited(documentCanvas.TextBox.Text);

                    documentCanvas.wordSplitterValue = documentCanvas.TextBox.Text;
                    documentCanvas.wordSplitterValueSplitStart = documentCanvas.TextBox.SelectionStart;
                    documentCanvas.wordSplitterValueSplitLength = documentCanvas.TextBox.SelectionLength;

                    documentCanvas.EndEditing();
                }
                else
                {
                    documentCanvas.wordSplitterValue = documentCanvas.SelectedItems?.First().InnerText ?? string.Empty;
                }

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        documentCanvas.Refresh();
    }

    private static void SelectionBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        documentCanvas.UpdateSelectionPopup();
    }

    private void AddSelectedElements(IEnumerable<HocrNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (selectedElements.Contains(node.Id))
            {
                continue;
            }

            foreach (var id in GetHierarchy(node))
            {
                selectedElements.Add(id);
            }
        }
    }

    private void RemoveSelectedElements(IEnumerable<HocrNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (!selectedElements.Contains(node.Id))
            {
                continue;
            }

            foreach (var id in GetHierarchy(node))
            {
                selectedElements.Remove(id);
            }
        }
    }

    private void NodesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    var isNewDocument = rootId < 0;

                    BuildDocumentElements(e.NewItems.Cast<HocrNodeViewModel>());

                    if (isNewDocument)
                    {
                        CenterTransformationDocument();
                    }
                }

                UpdateCanvasSelection();
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    var list = e.OldItems.Cast<HocrNodeViewModel>().ToList();

                    RemoveSelectedElements(list);

                    foreach (var node in list)
                    {
                        elements.Remove(node.Id, out var tuple);
                        elementPool.Return(tuple.Item2);
                    }
                }

                UpdateCanvasSelection();
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems != null)
                {
                    var list = e.OldItems.Cast<HocrNodeViewModel>().ToList();

                    foreach (var node in list)
                    {
                        elements.Remove(node.Id, out var tuple);
                        elementPool.Return(tuple.Item2);
                    }

                    BuildDocumentElements(list);
                }

                UpdateCanvasSelection();
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (var (_, element) in elements.Values)
                {
                    elementPool.Return(element);
                }

                elements.Clear();

                ClearCanvasSelection();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void SelectedNodesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems != null)
                {
                    AddSelectedElements(e.NewItems.Cast<HocrNodeViewModel>());
                }

                UpdateCanvasSelection();
                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    RemoveSelectedElements(e.OldItems.Cast<HocrNodeViewModel>());
                }

                UpdateCanvasSelection();
                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.NewItems != null)
                {
                    AddSelectedElements(e.NewItems.Cast<HocrNodeViewModel>());
                }

                if (e.OldItems != null)
                {
                    RemoveSelectedElements(e.OldItems.Cast<HocrNodeViewModel>());
                }

                UpdateCanvasSelection();
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Reset:
                ClearCanvasSelection();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        if (SelectedItems?.Count > 1)
        {
            ResetSelectionCycle();
        }

        Refresh();
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        Refresh();
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);

        Refresh();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (ViewModel?.Nodes is not { Count: > 0 })
        {
            return;
        }

        if (mouseMoveState != MouseState.None)
        {
            return;
        }

        Mouse.Capture(this);

        var position = e.GetPosition(this).ToSKPoint();

        switch (e.ChangedButton)
        {
            case MouseButton.Left:
            {
                e.Handled = true;

                dragStart = position;

                Keyboard.Focus(this);

                if (canvasSelection.ShouldShowCanvasSelection)
                {
                    var selectedHandle = canvasSelection.ResizeHandles
                        .FirstOrDefault(handle => handle.GetRect(transformation).Contains(position));

                    if (selectedHandle != null)
                    {
                        mouseMoveState = MouseState.Resizing;

                        CaptureKeyDownEvents();

                        canvasSelection.BeginResize();

                        selectedResizeHandle = selectedHandle;

                        offsetStart = selectedHandle.Center;

                        break;
                    }
                }

                var normalizedPosition = inverseTransformation.MapPoint(position);

                if (ActiveTool == DocumentCanvasTool.SelectionTool)
                {
                    if (canvasSelection.ShouldShowCanvasSelection &&
                        canvasSelection.Bounds.Contains(normalizedPosition))
                    {
                        mouseMoveState = MouseState.DraggingSelectionRegion;

                        var parentBounds = RootElement.Bounds;

                        dragLimit = SKRect.Create(
                            parentBounds.Width - canvasSelection.Bounds.Width,
                            parentBounds.Height - canvasSelection.Bounds.Height
                        );

                        offsetStart = transformation.MapPoint(canvasSelection.Bounds.Location);
                    }
                    else
                    {
                        mouseMoveState = MouseState.SelectingRegion;

                        EndEditing();

                        ClearSelection();

                        dragLimit = RootElement.Bounds;

                        var bounds = SKRect.Create(normalizedPosition, SKSize.Empty);

                        bounds.Clamp(dragLimit);

                        canvasSelection.Bounds = bounds;
                    }

                    break;
                }

                if (ActiveTool == DocumentCanvasTool.WordSplitTool)
                {
                    Ensure.IsValid(
                        nameof(SelectedItems),
                        SelectedItems?.Count == 1,
                        "Expected to have exactly one node selected"
                    );
                    Ensure.IsValid(
                        nameof(SelectedItems),
                        SelectedItems?.First().NodeType == HocrNodeType.Word,
                        "Expected selected node to be a word"
                    );

                    var selectedElement = elements[selectedElements.First()].Item2;

                    if (selectedElement.Bounds.Contains(normalizedPosition))
                    {
                        mouseMoveState = MouseState.DraggingWordSplitter;

                        wordSplitterPosition = normalizedPosition;
                    }

                    break;
                }

                EndEditing();

                // Dragging current the selection, no need to select anything else.
                if (canvasSelection.Bounds.Contains(normalizedPosition))
                {
                    BeginDrag();
                    break;
                }

                SelectNode(normalizedPosition);

                // Dragging the new selection, no need to select anything else.
                if (canvasSelection.Bounds.Contains(normalizedPosition))
                {
                    BeginDrag();
                    break;
                }

                if (!selectedElements.Any() && RootElement.Bounds.Contains(normalizedPosition))
                {
                    mouseMoveState = MouseState.SelectingNodes;

                    dragLimit = RootElement.Bounds;

                    var bounds = SKRect.Create(normalizedPosition, SKSize.Empty);

                    bounds.Clamp(dragLimit);

                    nodeSelection = bounds;

                    SelectNodesWithinRegion(bounds);
                }

                break;
            }
            case MouseButton.Middle:
            {
                e.Handled = true;

                dragStart = position;

                mouseMoveState = MouseState.Panning;

                offsetStart = transformation.MapPoint(SKPoint.Empty);

                break;
            }
            case MouseButton.Right:
            case MouseButton.XButton1:
            case MouseButton.XButton2:
            default:
                // Noop.
                return;
        }

        Refresh();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        var selectedItems = SelectedItems;
        if (selectedItems == null)
        {
            return;
        }

        switch (e.ChangedButton)
        {
            case MouseButton.Middle:
            {
                e.Handled = true;

                if (mouseMoveState != MouseState.Panning)
                {
                    return;
                }

                mouseMoveState = MouseState.None;
                break;
            }
            case MouseButton.Left:
            {
                e.Handled = true;

                var position = e.GetPosition(this).ToSKPoint();

                var mouseMoved = position != dragStart;

                switch (mouseMoveState)
                {
                    case MouseState.None:
                    case MouseState.Dragging:
                    {
                        if (!mouseMoved)
                        {
                            var normalizedPosition = inverseTransformation.MapPoint(position);

                            SelectNode(normalizedPosition);
                        }
                        break;
                    }
                    case MouseState.Panning:
                    {
                        break;
                    }
                    case MouseState.Resizing:
                    {
                        canvasSelection.EndResize();

                        ReleaseKeyDownEvents();
                        break;
                    }
                    case MouseState.SelectingNodes:
                    {
                        nodeSelection = SKRect.Empty;

                        break;
                    }
                    case MouseState.SelectingRegion:
                    {
                        canvasSelection.Bounds = canvasSelection.Bounds.Standardized;

                        SelectionBounds = Rect.FromSKRect(canvasSelection.Bounds);

                        break;
                    }
                    case MouseState.DraggingSelectionRegion:
                    {
                        SelectionBounds = Rect.FromSKRect(canvasSelection.Bounds);

                        break;
                    }
                    case MouseState.DraggingWordSplitter:
                    {
                        // TODO: Raise event?
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                mouseMoveState = MouseState.None;

                if (selectedElements.Any() && mouseMoved)
                {
                    var changes = new List<NodesChangedEventArgs.NodeChange>();

                    foreach (var id in selectedElements)
                    {
                        var (node, element) = elements[id];

                        changes.Add(new NodesChangedEventArgs.NodeChange(node, (Rect)element.Bounds, node.BBox));
                    }

                    OnNodesChanged(changes);

                    dragLimit = CalculateDragLimitBounds(selectedItems);
                }

                // Reset selection latch when a click cycle (mousedown-mouseup) is complete.
                selectionChangedLatch = false;

                break;
            }
            case MouseButton.Right:
            case MouseButton.XButton1:
            case MouseButton.XButton2:
            default:
                return;
        }

        ReleaseMouseCapture();

        Refresh();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var selectedItems = SelectedItems;
        if (selectedItems == null)
        {
            return;
        }

        var position = e.GetPosition(this).ToSKPoint();

        var delta = inverseScaleTransformation.MapPoint(position - dragStart);

        switch (mouseMoveState)
        {
            case MouseState.None:
            {
                if (!canvasSelection.ShouldShowCanvasSelection)
                {
                    return;
                }

                var resizeHandles = canvasSelection.ResizeHandles;

                var hoveringOnSelection = false;

                foreach (var handle in resizeHandles)
                {
                    var handleRect = handle.GetRect(transformation);

                    if (!handleRect.Contains(position))
                    {
                        continue;
                    }

                    Cursor = handle.Direction switch
                    {
                        CardinalDirections.NorthWest or CardinalDirections.SouthEast => Cursors.SizeNWSE,
                        CardinalDirections.North or CardinalDirections.South => Cursors.SizeNS,
                        CardinalDirections.NorthEast or CardinalDirections.SouthWest => Cursors.SizeNESW,
                        CardinalDirections.West or CardinalDirections.East => Cursors.SizeWE,
                        _ => throw new ArgumentOutOfRangeException(nameof(handle.Direction))
                    };

                    hoveringOnSelection = true;

                    break;
                }

                if (!hoveringOnSelection && transformation.MapRect(canvasSelection.Bounds).Contains(position))
                {
                    hoveringOnSelection = true;

                    Cursor = ActiveTool == DocumentCanvasTool.WordSplitTool ? Cursors.SizeWE : Cursors.SizeAll;
                }

                if (!hoveringOnSelection)
                {
                    Cursor = currentCursor;
                }

                // Skip refreshing.
                return;
            }
            case MouseState.Panning:
            {
                e.Handled = true;

                var newLocation = inverseTransformation.MapPoint(offsetStart) + delta;

                UpdateTransformation(SKMatrix.CreateTranslation(newLocation.X, newLocation.Y));

                break;
            }
            case MouseState.Dragging:
            {
                e.Handled = true;

                if (!dragLimit.IsEmpty)
                {
                    delta.Clamp(dragLimit);
                }

                var newLocation = inverseTransformation.MapPoint(offsetStart) + delta;

                if (selectedItems.Any())
                {
                    // Apply to all selected elements.
                    foreach (var id in selectedElements)
                    {
                        var (_, element) = elements[id];

                        var deltaFromDraggedElement = element.Bounds.Location - canvasSelection.Bounds.Location;

                        element.Bounds = element.Bounds with
                        {
                            Location = newLocation + deltaFromDraggedElement
                        };
                    }

                    // Apply to selection rect.
                    canvasSelection.Bounds = canvasSelection.Bounds with
                    {
                        Location = newLocation
                    };
                }

                break;
            }
            case MouseState.Resizing:
            {
                PerformResize(delta);

                break;
            }
            case MouseState.SelectingNodes:
            {
                var newLocation = inverseTransformation.MapPoint(dragStart) + delta;

                newLocation.Clamp(dragLimit);

                nodeSelection.Right = newLocation.X;
                nodeSelection.Bottom = newLocation.Y;

                var selection = SelectNodesWithinRegion(nodeSelection);

                IList removed = Array.Empty<HocrNodeViewModel>();

                if (SelectedItems is { Count: > 0 })
                {
                    removed = SelectedItems.Except(selection).ToList();

                    selection.ExceptWith(SelectedItems);
                }

                OnSelectionChanged(
                    new SelectionChangedEventArgs(
                        Selector.SelectionChangedEvent,
                        removed,
                        selection.ToList()
                    )
                );

                break;
            }
            case MouseState.SelectingRegion:
            {
                var newLocation = inverseTransformation.MapPoint(dragStart) + delta;

                newLocation.Clamp(dragLimit);

                canvasSelection.Right = newLocation.X;
                canvasSelection.Bottom = newLocation.Y;

                break;
            }
            case MouseState.DraggingSelectionRegion:
            {
                var newLocation = inverseTransformation.MapPoint(offsetStart) + delta;

                newLocation.Clamp(dragLimit);

                var newBounds = canvasSelection.Bounds with
                {
                    Location = newLocation
                };

                canvasSelection.Bounds = newBounds;

                break;
            }
            case MouseState.DraggingWordSplitter:
            {
                var newLocation = inverseTransformation.MapPoint(dragStart) + delta;

                var selectedElement = elements[selectedElements.First()].Item2;

                newLocation.Clamp(selectedElement.Bounds);

                wordSplitterPosition = newLocation;

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }

        Refresh();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var selectedItems = SelectedItems;
        if (selectedItems == null)
        {
            return;
        }

        var delta = Math.Sign(e.Delta) * 3;

        var pointerP = e.GetPosition(this).ToSKPoint();
        var p = inverseTransformation.MapPoint(pointerP);

        var newScale = (float)Math.Pow(2, delta * 0.05f);

        UpdateTransformation(SKMatrix.CreateScale(newScale, newScale, p.X, p.Y));

        dragLimit = CalculateDragLimitBounds(selectedItems);

        Refresh();
    }

    private void ResetSelectionCycle()
    {
        selectedKey = -1;
        selectedKeyCandidates.Clear();
    }

    private void SelectNode(SKPoint normalizedPosition)
    {
        if (selectionChangedLatch)
        {
            return;
        }

        // Get keys for all nodes overlapping at this point.
        var newKeyCandidates = GetVisibleElementKeysAtPoint(normalizedPosition);

        if (!newKeyCandidates.Any())
        {
            ClearSelection();

            ResetSelectionCycle();

            return;
        }

        // Keep track of the current list of candidates. If they're the same from one click to another, we cycle through
        //  them. Otherwise, we replace the list and start over.
        if (newKeyCandidates.SequenceEqual(selectedKeyCandidates))
        {
            var index = selectedKeyCandidates.IndexOf(selectedKey);

            selectedKey = selectedKeyCandidates[(index + 1) % selectedKeyCandidates.Count];
        }
        else
        {
            selectedKeyCandidates = newKeyCandidates;

            selectedKey = PickFirstSelectionCandidate(selectedKeyCandidates);
        }

        var node = elements[selectedKey].Item1;

        //  i.e. about to drag selection or choose a different item
        var selectedNodes = SelectedItems ??
                            throw new InvalidOperationException("Expected ViewModel to not be null");

        // Page is unselectable.
        if (node.NodeType == HocrNodeType.Page)
        {
            ClearSelection();

            return;
        }

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ClearSelection();
        }

        if (selectedNodes.Contains(node))
        {
            RemoveSelectedNode(node);
        }
        else
        {
            AddSelectedNode(node);
        }

        selectionChangedLatch = true;

        UpdateCanvasSelection();
    }

    private void BeginDrag()
    {
        mouseMoveState = MouseState.Dragging;

        offsetStart = transformation.MapPoint(canvasSelection.Bounds.Location);

        dragLimit = CalculateDragLimitBounds(SelectedItems ?? throw new InvalidOperationException());
    }

    private HashSet<HocrNodeViewModel> SelectNodesWithinRegion(SKRect selection)
    {
        if (rootId < 0)
        {
            throw new InvalidOperationException($"Expected {rootId} to be greater or equal to 0.");
        }

        selection = transformation.MapRect(selection);

        var selectedNodes = new HashSet<HocrNodeViewModel>();

        void Recurse(int key)
        {
            var (node, element) = elements[key];

            var bounds = transformation.MapRect(element.Bounds);

            if (!selection.IntersectsWithInclusive(bounds))
            {
                return;
            }

            if (!node.IsRoot)
            {
                selectedNodes.Add(node);
            }

            foreach (var childKey in node.Children.Select(c => c.Id))
            {
                Recurse(childKey);
            }
        }

        Recurse(rootId);

        return selectedNodes;
    }

    // Pick the node with the smallest area, as it's likely the most specific one.
    //  (e.g. word rather than line or paragraph)
    private int PickFirstSelectionCandidate(IEnumerable<int> candidates) =>
        candidates.MinBy(
            k =>
            {
                var node = elements[k].Item1;

                return node.BBox.Width * node.BBox.Height;
            }
        );

    private void AddSelectedNode(HocrNodeViewModel node)
    {
        OnSelectionChanged(
            new SelectionChangedEventArgs(
                Selector.SelectionChangedEvent,
                Array.Empty<HocrNodeViewModel>(),
                new List<HocrNodeViewModel> { node }
            )
        );
    }

    private void RemoveSelectedNode(HocrNodeViewModel node)
    {
        OnSelectionChanged(
            new SelectionChangedEventArgs(
                Selector.SelectionChangedEvent,
                new List<HocrNodeViewModel> { node },
                Array.Empty<HocrNodeViewModel>()
            )
        );
    }

    private void UpdateCanvasSelection()
    {
        var allNodes = selectedElements.Select(id => elements[id].Item1).ToList();

        canvasSelection.Bounds = NodeHelpers.CalculateUnionRect(allNodes).ToSKRect();

        // TODO: Support for multiple selection.
        // If we have only one item selected, set its resize limits to within its parent and around its children.
        if (SelectedItems?.Count == 1)
        {
            var node = SelectedItems.First();

            var containedChildren = node.Children.Where(c => node.BBox.Contains(c.BBox));

            resizeLimitInside = NodeHelpers.CalculateUnionRect(containedChildren).ToSKRect();

            // TODO: This fails when merging.
            // Debug.Assert(
            //     resizeLimitInside.IsEmpty || canvasSelection.Bounds.Contains(resizeLimitInside),
            //     "Expected inner resize limit to be contained in the canvas selection bounds."
            // );

            if (node.ParentId >= 0)
            {
                resizeLimitOutside = elements[node.ParentId].Item2.Bounds;

                // TODO: This fails when merging.
                // Debug.Assert(
                //     resizeLimitOutside.Contains(canvasSelection.Bounds),
                //     "Expected outer resize limit to contain the canvas selection bounds."
                // );
            }
        }
        else
        {
            // No resize limit (any size within the page).
            ClearCanvasResizeLimit();
        }
    }

    private void ClearCanvasSelection()
    {
        if (rootId < 0)
        {
            return;
        }

        dragLimit = SKRect.Empty;
        canvasSelection.Bounds = SKRect.Empty;

        SelectionBounds = Rect.Empty;

        ClearCanvasResizeLimit();

        selectedElements.Clear();
    }

    private void ClearCanvasResizeLimit()
    {
        resizeLimitInside = SKRect.Empty;
        resizeLimitOutside = RootElement.Bounds;
    }

    private void ClearSelection()
    {
        var selectedItems = SelectedItems;

        if (selectedItems == null)
        {
            return;
        }

        OnSelectionChanged(
            new SelectionChangedEventArgs(
                Selector.SelectionChangedEvent,
                selectedItems.ToList(),
                Array.Empty<HocrNodeViewModel>()
            )
        );

        ClearCanvasSelection();
    }

    private void ResetTransformation()
    {
        transformation = inverseTransformation = scaleTransformation = inverseScaleTransformation = SKMatrix.Identity;
    }

    private void UpdateTransformation(SKMatrix matrix)
    {
        const float transformationScaleMin = 1 / 32.0f;
        const float transformationScaleMax = 8.0f;

        var nextTransformation = transformation.PreConcat(matrix);

        // TODO: Clamping to the exact zoom limits is not as straightforward as setting the scale, as the translation
        //  needs to adapt. Figure it out.
        if (nextTransformation.ScaleX < transformation.ScaleX &&
            nextTransformation.ScaleX < transformationScaleMin)
        {
            return;
        }

        if (nextTransformation.ScaleX > transformation.ScaleX &&
            nextTransformation.ScaleX > transformationScaleMax)
        {
            return;
        }

        transformation = nextTransformation;

        inverseTransformation = transformation.Invert();

        scaleTransformation.ScaleX = transformation.ScaleX;
        scaleTransformation.ScaleY = transformation.ScaleY;
        inverseScaleTransformation.ScaleX = inverseTransformation.ScaleX;
        inverseScaleTransformation.ScaleY = inverseTransformation.ScaleY;
    }

    private void CenterTransformationDocument()
    {
        var documentBounds = RootElement.Bounds;

        var controlSize = SKRect.Create(RenderSize.ToSKSize());

        controlSize.Inflate(CenterPadding);

        var fitBounds = controlSize.AspectFit(documentBounds.Size);

        var resizeFactor = Math.Min(
            fitBounds.Width / documentBounds.Width,
            fitBounds.Height / documentBounds.Height
        );

        CenterTransformation(documentBounds, resizeFactor);
    }

    private void CenterTransformationSelection()
    {
        var controlSize = SKRect.Create(RenderSize.ToSKSize());

        controlSize.Inflate(CenterPadding);

        var fitBounds = controlSize.AspectFit(canvasSelection.Size);

        var resizeFactor = Math.Min(
            fitBounds.Width / canvasSelection.Width,
            fitBounds.Height / canvasSelection.Height
        );

        resizeFactor = (float)Math.Log(1.0f + resizeFactor) * 0.33f;

        CenterTransformation(canvasSelection.Bounds, resizeFactor);
    }

    private void CenterTransformation(SKRect rect, float resizeFactor)
    {
        ResetTransformation();

        var scaleMatrix = SKMatrix.CreateScale(
            resizeFactor,
            resizeFactor
        );

        var controlSize = SKRect.Create(RenderSize.ToSKSize());

        controlSize.Inflate(CenterPadding);

        rect = scaleMatrix.MapRect(rect);

        UpdateTransformation(
            SKMatrix.CreateTranslation(controlSize.MidX - rect.MidX, controlSize.MidY - rect.MidY)
                .PreConcat(scaleMatrix)
        );
    }

    private void Refresh()
    {
        Dispatcher.InvokeAsync(Surface.InvalidateVisual, DispatcherPriority.Render);

        UpdateTextBox();

        UpdateSelectionPopup();
    }

    private void BuildDocumentElements(IEnumerable<HocrNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            var el = elementPool.Get();
            el.Bounds = node.BBox.ToSKRect();

            elements.Add(node.Id, (node, el));

            if (node.HocrNode is HocrPage)
            {
                rootId = node.Id;
            }
        }
    }

    private void Canvas_OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.LightGray);

        RenderCanvas(canvas);
    }

    private void PerformResize(SKPoint delta)
    {
        var newLocation = offsetStart + delta;

        Debug.Assert(selectedResizeHandle != null, $"{nameof(selectedResizeHandle)} != null");

        var resizePivot = new SKPoint(canvasSelection.InitialBounds.MidX, canvasSelection.InitialBounds.MidY);

        // If more than one element selected, or exactly one element selected _and_ Ctrl is pressed, resize together with children.
        var resizeWithChildren = SelectedItems?.Count > 1 ||
                                 Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        var resizeSymmetrical = Keyboard.Modifiers.HasFlag(ModifierKeys.Alt);

        // Reset the selection bounds so changing keyboard modifiers works off of the initial bounds.
        // This achieves a more "Photoshop-like" behavior for the selection.
        canvasSelection.Bounds = canvasSelection.InitialBounds;

        if (selectedResizeHandle.Direction.HasFlag(CardinalDirections.West))
        {
            // Calculate next left position for bounds while clamping within the limits.
            var nextLeft = Math.Clamp(
                newLocation.X,
                resizeLimitOutside.Left,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Right
                    : resizeLimitInside.Left
            );

            if (resizeSymmetrical)
            {
                // Calculate the symmetrical offset based on delta from the pivot.
                var deltaX = resizePivot.X - nextLeft;

                // Calculate next right position from the expected left position, but clamp within the limits.
                canvasSelection.Right = Math.Clamp(
                    nextLeft + 2 * deltaX,
                    resizeLimitInside.IsEmpty || resizeWithChildren
                        ? resizeLimitOutside.Left
                        : resizeLimitInside.Right,
                    resizeLimitOutside.Right
                );

                // Calculate delta to pivot from the opposite side of the symmetry.
                deltaX = canvasSelection.Right - resizePivot.X;

                // Recalculate left position, to achieve bounds that are clamped by the edge closest to the limits.
                nextLeft = resizePivot.X - deltaX;
            }
            else
            {
                resizePivot.X = canvasSelection.InitialBounds.Right;
            }

            canvasSelection.Left = nextLeft;
        }

        if (selectedResizeHandle.Direction.HasFlag(CardinalDirections.North))
        {
            var nextTop = Math.Clamp(
                newLocation.Y,
                resizeLimitOutside.Top,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Bottom
                    : resizeLimitInside.Top
            );

            if (resizeSymmetrical)
            {
                var deltaY = resizePivot.Y - nextTop;

                canvasSelection.Bottom = Math.Clamp(
                    nextTop + 2 * deltaY,
                    resizeLimitInside.IsEmpty || resizeWithChildren
                        ? resizeLimitOutside.Top
                        : resizeLimitInside.Bottom,
                    resizeLimitOutside.Bottom
                );

                deltaY = canvasSelection.Bottom - resizePivot.Y;

                nextTop = resizePivot.Y - deltaY;
            }
            else
            {
                resizePivot.Y = canvasSelection.InitialBounds.Bottom;
            }

            canvasSelection.Top = nextTop;
        }

        if (selectedResizeHandle.Direction.HasFlag(CardinalDirections.East))
        {
            var nextRight = Math.Clamp(
                newLocation.X,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Left
                    : resizeLimitInside.Right,
                resizeLimitOutside.Right
            );

            if (resizeSymmetrical)
            {
                var deltaX = nextRight - resizePivot.X;

                canvasSelection.Left = Math.Clamp(
                    nextRight - 2 * deltaX,
                    resizeLimitOutside.Left,
                    resizeLimitInside.IsEmpty || resizeWithChildren
                        ? resizeLimitOutside.Right
                        : resizeLimitInside.Left
                );

                deltaX = resizePivot.X - canvasSelection.Left;

                nextRight = resizePivot.X + deltaX;
            }
            else
            {
                resizePivot.X = canvasSelection.InitialBounds.Left;
            }

            canvasSelection.Right = nextRight;
        }

        if (selectedResizeHandle.Direction.HasFlag(CardinalDirections.South))
        {
            var nextBottom = Math.Clamp(
                newLocation.Y,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Top
                    : resizeLimitInside.Bottom,
                resizeLimitOutside.Bottom
            );

            if (resizeSymmetrical)
            {
                var deltaY = nextBottom - resizePivot.Y;

                canvasSelection.Top = Math.Clamp(
                    nextBottom - 2 * deltaY,
                    resizeLimitOutside.Top,
                    resizeLimitInside.IsEmpty || resizeWithChildren
                        ? resizeLimitOutside.Bottom
                        : resizeLimitInside.Top
                );

                deltaY = resizePivot.Y - canvasSelection.Top;

                nextBottom = resizePivot.Y + deltaY;
            }
            else
            {
                resizePivot.Y = canvasSelection.InitialBounds.Top;
            }

            canvasSelection.Bottom = nextBottom;
        }

        var ratio = canvasSelection.ResizeRatio;

        var matrix = SKMatrix.CreateScale(ratio.X, ratio.Y, resizePivot.X, resizePivot.Y);

        foreach (var id in selectedElements)
        {
            // Start with the initial value, so pressing and releasing Ctrl reverts to original size.
            var bounds = elements[id].Item1.BBox.ToSKRect();

            if (resizeWithChildren || (SelectedItems != null && SelectedItems.Any(node => node.Id == id)))
            {
                bounds = matrix.MapRect(bounds);
                bounds.Clamp(canvasSelection.Bounds);
            }

            elements[id].Item2.Bounds = bounds;
        }
    }

    private List<int> GetVisibleElementKeysAtPoint(SKPoint p)
    {
        bool NodeIsVisible(int key)
        {
            var node = elements[key].Item1;

            var visible = nodeVisibilityDictionary[node.NodeType];

            return visible;
        }

        return elements.Keys
            .Where(NodeIsVisible)
            .Where(k => elements[k].Item1.NodeType != HocrNodeType.Page)
            .Where(k => elements[k].Item2.Bounds.Contains(p))
            .OrderByDescending(k => elements[k].Item1.NodeType)
            .ToList();
    }

    private static IEnumerable<int> GetHierarchy(
        HocrNodeViewModel node
    ) =>
        node.Descendants
            .Prepend(node)
            .Select(n => n.Id);


    private static SKRect CalculateDragLimitBounds(
        IEnumerable<HocrNodeViewModel> selectedNodes
    )
    {
        var dragLimit = SKRect.Empty;

        foreach (var node in selectedNodes)
        {
            if (node.Parent == null)
            {
                continue;
            }

            var parentNode = node.Parent;

            var parentBounds = parentNode.BBox.ToSKRect();
            var nodeBounds = node.BBox.ToSKRect();

            // In some cases, the child node isn't contained within its parent. In that case, don't limit dragging for it (leave limit empty).
            if (parentBounds.Contains(nodeBounds))
            {
                var limitRect = new SKRect(
                    parentBounds.Left - nodeBounds.Left,
                    parentBounds.Top - nodeBounds.Top,
                    parentBounds.Right - nodeBounds.Right,
                    parentBounds.Bottom - nodeBounds.Bottom
                );

                if (dragLimit.IsEmpty)
                {
                    dragLimit = limitRect;
                }
                else
                {
                    dragLimit.Intersect(limitRect);
                }
            }
        }

        return dragLimit;
    }

    private void CaptureKeyDownEvents()
    {
        ParentWindow.KeyDown += WindowOnKeyChange;
        ParentWindow.KeyUp += WindowOnKeyChange;
    }

    private void ReleaseKeyDownEvents()
    {
        ParentWindow.KeyDown -= WindowOnKeyChange;
        ParentWindow.KeyUp -= WindowOnKeyChange;
    }

    private void WindowOnKeyChange(object sender, KeyEventArgs e)
    {
        // TODO: Mac support?
        if (e.Key is not (Key.LeftCtrl or Key.RightCtrl) || mouseMoveState != MouseState.Resizing)
        {
            return;
        }

        var position = Mouse.GetPosition(this).ToSKPoint();

        var delta = inverseScaleTransformation.MapPoint(position - dragStart);

        PerformResize(delta);

        Refresh();
    }

    private void OnNodesChanged(IList<NodesChangedEventArgs.NodeChange> changes)
    {
        RaiseEvent(
            new NodesChangedEventArgs(
                NodesChangedEvent,
                this,
                changes
            )
        );
    }

    private void OnNodeEdited(string value)
    {
        RaiseEvent(
            new NodesEditedEventArgs(
                NodesEditedEvent,
                this,
                SelectionHelper.SelectAllEditable(SelectedItems ?? Enumerable.Empty<HocrNodeViewModel>()),
                value
            )
        );
    }

    private void OnWordSplit(HocrNodeViewModel node, int splitPosition, (string, string) words)
    {
        RaiseEvent(
            new WordSplitEventArgs(
                WordSplitEvent,
                this,
                node,
                splitPosition,
                words
            )
        );
    }

    private void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        SelectionChanged?.Invoke(this, e);
    }

    private void TextBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (IsEditing)
        {
            OnNodeEdited(TextBox.Text);
        }

        EndEditing();
    }

    private void TextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Return or Key.Escape))
        {
            return;
        }

        e.Handled = true;

        EndEditing();

        if (e.Key == Key.Escape)
        {
            return;
        }

        OnNodeEdited(TextBox.Text);
    }

    private void BeginEditing()
    {
        var selectedItem = SelectionHelper.SelectEditable(SelectedItems ?? Enumerable.Empty<HocrNodeViewModel>());

        if (selectedItem == null)
        {
            return;
        }

        editingNode = selectedItem;

        editingNode.IsEditing = true;

        var paragraph = (HocrParagraph?)editingNode.FindParent(HocrNodeType.Paragraph)?.HocrNode;

        TextBox.Text = editingNode.InnerText;

        TextBox.Visibility = Visibility.Visible;
        TextBox.FlowDirection =
            paragraph?.Direction == Direction.Rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        UpdateTextBox();
    }

    private void UpdateTextBox() => Dispatcher.BeginInvoke(
        () =>
        {
            if (editingNode == null)
            {
                return;
            }

            var element = elements[editingNode!.Id].Item2;
            var rect = transformation.MapRect(element.Bounds);

            var word = (HocrWord)editingNode.HocrNode;
            var line = (HocrLine)elements[word.ParentId].Item1.HocrNode;

            Canvas.SetLeft(TextBox, (int)rect.Left);
            Canvas.SetTop(TextBox, (int)rect.Top);
            TextBox.Width = rect.Width;
            TextBox.Height = rect.Height;

            var fontSize = transformation.ScaleX * line.FontSize * 0.6f;

            TextBox.FontSize = fontSize;
            TextBlock.SetLineHeight(TextBox, fontSize);

            TextBox.Focus();
            TextBox.SelectAll();
        },
        DispatcherPriority.Render
    );

    private void UpdateSelectionPopup() => Dispatcher.BeginInvoke(
        () =>
        {
            var bounds = transformation.MapRect(canvasSelection.Bounds);

            SelectionPopup.Visibility =
                canvasSelection.ShouldShowCanvasSelection && ActiveTool == DocumentCanvasTool.SelectionTool
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            Canvas.SetLeft(SelectionPopup, (int)bounds.Left);
            Canvas.SetTop(SelectionPopup, (int)(bounds.Top - SelectionPopup.ActualHeight));
        },
        DispatcherPriority.Render
    );

    private void EndEditing()
    {
        TextBox.Visibility = Visibility.Collapsed;

        if (editingNode != null)
        {
            editingNode.IsEditing = false;
            editingNode = null;
        }
    }
}
