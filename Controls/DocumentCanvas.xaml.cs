using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
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
using SkiaSharp.HarfBuzz;
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
    Selecting,
    DraggingSelection,
}

public class Element
{
    public SKBitmap? Background { get; set; }
    public SKRect Bounds { get; set; }
}

public partial class DocumentCanvas
{
    private static readonly SKSize CenterPadding = new(-10.0f, -10.0f);

    private static readonly SKPaint HandleFillPaint = new()
    {
        IsStroke = false,
        Color = SKColors.White,
        StrokeWidth = 1,
    };

    private static readonly SKPaint HandleStrokePaint = new()
    {
        IsStroke = true,
        Color = SKColors.Gray,
        StrokeWidth = 1,
    };

    private static SKColor GetNodeColor(HocrNodeViewModel node) => node.NodeType switch
    {
        // Node colors based on the D3 color category "Paired": https://observablehq.com/@d3/color-schemes
        // ["#a6cee3","#1f78b4","#b2df8a","#33a02c","#fb9a99","#e31a1c","#fdbf6f","#ff7f00","#cab2d6","#6a3d9a","#ffff99","#b15928"]
        HocrNodeType.Page => SKColor.Empty,
        HocrNodeType.ContentArea => new SKColor(0xffa6cee3),
        HocrNodeType.Paragraph => new SKColor(0xffb2df8a),
        HocrNodeType.Line or HocrNodeType.TextFloat or HocrNodeType.Caption => new SKColor(0xfffdbf6f),
        HocrNodeType.Word => new SKColor(0xfffb9a99),
        HocrNodeType.Image => new SKColor(0xffcab2d6),
        _ => throw new ArgumentOutOfRangeException()
    };

    private readonly ObjectPool<Element> elementPool =
        new DefaultObjectPool<Element>(new DefaultPooledObjectPolicy<Element>());

    public static readonly DependencyProperty SelectedItemsProperty = DependencyProperty.Register(
        nameof(SelectedItems),
        typeof(ObservableHashSet<HocrNodeViewModel>),
        typeof(DocumentCanvas),
        new PropertyMetadata(
            null,
            SelectedItemsChanged
        )
    );

    public static readonly DependencyProperty ItemsSourceProperty
        = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<HocrNodeViewModel>),
            typeof(DocumentCanvas),
            new PropertyMetadata(
                null,
                ItemsSourceChanged
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

    public static readonly DependencyProperty IsShowTextProperty = DependencyProperty.Register(
        nameof(IsShowText),
        typeof(bool),
        typeof(DocumentCanvas),
        new PropertyMetadata(
            false,
            IsShowTextChanged
        )
    );

    public static readonly DependencyProperty IsSelectingProperty = DependencyProperty.Register(
        nameof(IsSelecting),
        typeof(bool),
        typeof(DocumentCanvas),
        new FrameworkPropertyMetadata(
            default(bool),
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            IsSelectingChanged
        )
    );

    public static readonly DependencyProperty SelectionBoundsProperty = DependencyProperty.Register(
        nameof(SelectionBounds),
        typeof(Rect),
        typeof(DocumentCanvas),
        new PropertyMetadata(default(Rect))
    );

    public static readonly RoutedEvent NodesEditedEvent = EventManager.RegisterRoutedEvent(
        "NodesEdited",
        RoutingStrategy.Bubble,
        typeof(EventHandler<NodesEditedEventArgs>),
        typeof(DocumentCanvas)
    );

    public ObservableHashSet<HocrNodeViewModel>? SelectedItems
    {
        get => (ObservableHashSet<HocrNodeViewModel>?)GetValue(SelectedItemsProperty);
        set => SetValue(SelectedItemsProperty, value);
    }

    public ObservableCollection<HocrNodeViewModel>? ItemsSource
    {
        get => (ObservableCollection<HocrNodeViewModel>)GetValue(ItemsSourceProperty);
        set
        {
            if (value == null)
            {
                ClearValue(ItemsSourceProperty);
            }
            else
            {
                SetValue(ItemsSourceProperty, value);
            }
        }
    }

    public ReadOnlyObservableCollection<NodeVisibility> NodeVisibility
    {
        get => (ReadOnlyObservableCollection<NodeVisibility>)GetValue(NodeVisibilityProperty);
        set => SetValue(NodeVisibilityProperty, value);
    }

    public bool IsShowText
    {
        get => (bool)GetValue(IsShowTextProperty);
        set => SetValue(IsShowTextProperty, value);
    }

    public bool IsSelecting
    {
        get => (bool)GetValue(IsSelectingProperty);
        set => SetValue(IsSelectingProperty, value);
    }

    public Rect SelectionBounds
    {
        get => (Rect)GetValue(SelectionBoundsProperty);
        set => SetValue(SelectionBoundsProperty, value);
    }

    private int rootId = -1;
    private readonly Dictionary<int, (HocrNodeViewModel, Element)> elements = new();

    private HocrNodeViewModel RootNode => elements[rootId].Item1;

    private Element RootElement => elements[rootId].Item2;

    private Dictionary<HocrNodeType, bool> nodeVisibilityDictionary = new();

    private readonly HashSet<int> selectedElements = new();

    private HocrNodeViewModel? editingNode;

    private bool IsEditing => editingNode != null;

    private SKMatrix transformation = SKMatrix.Identity;
    private SKMatrix inverseTransformation = SKMatrix.Identity;
    private SKMatrix scaleTransformation = SKMatrix.Identity;
    private SKMatrix inverseScaleTransformation = SKMatrix.Identity;

    private MouseState mouseMoveState;

    private SKPoint dragStart;
    private SKPoint offsetStart;

    private SKRect dragLimit = SKRect.Empty;

    private SKRect resizeLimitInside = SKRect.Empty;
    private SKRect resizeLimitOutside = SKRect.Empty;

    private Cursor? currentCursor;
    private readonly CanvasSelection canvasSelection = new();
    private ResizeHandle? selectedResizeHandle;

    public event EventHandler<NodesChangedEventArgs>? NodesChanged;

    public event EventHandler<NodesEditedEventArgs> NodesEdited
    {
        add => AddHandler(NodesEditedEvent, value);
        remove => RemoveHandler(NodesEditedEvent, value);
    }

    public event SelectionChangedEventHandler? SelectionChanged;

    private Window? parentWindow;
    private Window ParentWindow => parentWindow ??= Window.GetWindow(this) ?? throw new InvalidOperationException();

    public DocumentCanvas()
    {
        InitializeComponent();

        ClipToBounds = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ParentWindow.KeyDown += WindowOnKeyDown;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ParentWindow.KeyDown -= WindowOnKeyDown;

        HandleFillPaint.Dispose();
        HandleStrokePaint.Dispose();
    }

    private void WindowOnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Return when IsFocused:
            {
                BeginEditing();
                break;
            }
            case Key.Escape when IsSelecting:
            {
                IsSelecting = false;
                break;
            }
        }
    }

    private static void ItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        foreach (var (_, element) in documentCanvas.elements.Values)
        {
            documentCanvas.elementPool.Return(element);
        }

        documentCanvas.elements.Clear();

        if (e.OldValue is ObservableCollection<HocrNodeViewModel> oldNodes && oldNodes.Any())
        {
            oldNodes.UnsubscribeItemPropertyChanged(documentCanvas.NodesOnItemPropertyChanged);

            oldNodes.CollectionChanged -= documentCanvas.NodesOnCollectionChanged;
        }

        if (e.NewValue is ObservableCollection<HocrNodeViewModel> newNodes && newNodes.Any())
        {
            var rootNode = newNodes[0];

            documentCanvas.BuildDocumentElements(rootNode.Descendents.Prepend(rootNode));

            newNodes.SubscribeItemPropertyChanged(documentCanvas.NodesOnItemPropertyChanged);

            newNodes.CollectionChanged += documentCanvas.NodesOnCollectionChanged;

            documentCanvas.CenterTransformation();

            documentCanvas.Refresh();
        }
    }

    private void NodesOnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Ensure.IsNotNull(nameof(sender), sender);

        var node = (HocrNodeViewModel)sender!;

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

    private static void SelectedItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        if (documentCanvas.SelectedItems == null)
        {
            return;
        }

        documentCanvas.SelectedItems.CollectionChanged += documentCanvas.SelectedNodesOnCollectionChanged;

        documentCanvas.Refresh();
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
                newNodes.Select(nv => new KeyValuePair<HocrNodeType, bool>(nv.NodeType, nv.Visible))
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

        nodeVisibilityDictionary[nodeVisibility.NodeType] = nodeVisibility.Visible;

        Refresh();
    }

    private static void IsShowTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        documentCanvas.Refresh();
    }

    private static void IsSelectingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        var newValue = (bool)e.NewValue;

        if (newValue)
        {
            documentCanvas.Cursor = documentCanvas.currentCursor = Cursors.Cross;
        }
        else
        {
            documentCanvas.Cursor = documentCanvas.currentCursor = null;

            documentCanvas.ClearCanvasSelection();

            documentCanvas.Refresh();
        }
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
                    BuildDocumentElements(e.NewItems.Cast<HocrNodeViewModel>());
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

        if (ItemsSource is not { Count: > 0 })
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

                if (!canvasSelection.IsEmpty)
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

                if (IsSelecting)
                {
                    if (!canvasSelection.IsEmpty && canvasSelection.Bounds.Contains(normalizedPosition))
                    {
                        mouseMoveState = MouseState.DraggingSelection;

                        var parentBounds = RootElement.Bounds;

                        dragLimit = SKRect.Create(
                            parentBounds.Width - canvasSelection.Bounds.Width,
                            parentBounds.Height - canvasSelection.Bounds.Height
                        );

                        offsetStart = transformation.MapPoint(canvasSelection.Bounds.Location);
                    }
                    else
                    {
                        mouseMoveState = MouseState.Selecting;

                        EndEditing();

                        ClearSelection();

                        dragLimit = RootElement.Bounds;

                        var bounds = SKRect.Create(normalizedPosition, SKSize.Empty);

                        bounds.Clamp(dragLimit);

                        canvasSelection.Bounds = bounds;
                    }

                    break;
                }

                mouseMoveState = MouseState.Dragging;

                if (canvasSelection.Bounds.Contains(normalizedPosition))
                {
                    // Dragging the selection, no need to select anything else.
                    offsetStart = transformation.MapPoint(canvasSelection.Bounds.Location);

                    break;
                }

                EndEditing();

                SelectNode(normalizedPosition);

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

                switch (mouseMoveState)
                {
                    case MouseState.None:
                    case MouseState.Dragging:
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
                    case MouseState.Selecting:
                    {
                        canvasSelection.Bounds = canvasSelection.Bounds.Standardized;

                        SelectionBounds = new Rect(
                            (int)canvasSelection.Left,
                            (int)canvasSelection.Top,
                            (int)canvasSelection.Right,
                            (int)canvasSelection.Bottom
                        );

                        break;
                    }
                    case MouseState.DraggingSelection:
                    {
                        SelectionBounds = new Rect(
                            (int)canvasSelection.Left,
                            (int)canvasSelection.Top,
                            (int)canvasSelection.Right,
                            (int)canvasSelection.Bottom
                        );

                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }


                mouseMoveState = MouseState.None;

                var mouseMoved = position != dragStart;

                if (selectedElements.Any() && mouseMoved)
                {
                    var changes = new List<NodesChangedEventArgs.NodeChange>();

                    foreach (var id in selectedElements)
                    {
                        var (node, element) = elements[id];

                        changes.Add(new NodesChangedEventArgs.NodeChange(node, (Rect)element.Bounds, node.BBox));
                    }

                    OnNodesChanged(new NodesChangedEventArgs(changes));

                    dragLimit = CalculateDragLimitBounds(selectedItems);
                }

                if (!mouseMoved)
                {
                    SelectNode(inverseTransformation.MapPoint(position));
                }

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
                if (!canvasSelection.IsEmpty)
                {
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

                        Cursor = Cursors.SizeAll;
                    }

                    if (!hoveringOnSelection)
                    {
                        Cursor = currentCursor;
                    }
                }

                // Skip refreshing.
                return;
            }
            case MouseState.Panning:
            {
                e.Handled = true;

                var newLocation = inverseTransformation.MapPoint(offsetStart) + delta;

                UpdateTransformation(SKMatrix.CreateTranslation(newLocation.X, newLocation.Y));

                if (editingNode != null)
                {
                    Dispatcher.InvokeAsync(UpdateTextBox, DispatcherPriority.Render);
                }

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

                if (editingNode != null)
                {
                    Dispatcher.InvokeAsync(UpdateTextBox, DispatcherPriority.Render);
                }

                break;
            }
            case MouseState.Resizing:
            {
                PerformResize(delta);

                if (editingNode != null)
                {
                    Dispatcher.InvokeAsync(UpdateTextBox, DispatcherPriority.Render);
                }

                break;
            }
            case MouseState.Selecting:
            {
                var newLocation = inverseTransformation.MapPoint(dragStart) + delta;

                newLocation.Clamp(dragLimit);

                canvasSelection.Right = newLocation.X;
                canvasSelection.Bottom = newLocation.Y;

                break;
            }
            case MouseState.DraggingSelection:
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

        var newScale = (float)Math.Pow(2, delta * 0.05);

        UpdateTransformation(SKMatrix.CreateScale(newScale, newScale, p.X, p.Y));

        dragLimit = CalculateDragLimitBounds(selectedItems);

        Refresh();

        if (editingNode != null)
        {
            Dispatcher.InvokeAsync(UpdateTextBox, DispatcherPriority.Render);
        }
    }

    private void SelectNode(SKPoint normalizedPosition)
    {
        var key = GetElementKeyAtPoint(normalizedPosition);

        if (key < 0)
        {
            ClearSelection();

            return;
        }

        var node = elements[key].Item1;

        foreach (var ascendant in node.Ascendants)
        {
            var (parentNode, parentElement) = elements[ascendant.Id];

            if (node.BBox.Equals(parentNode.BBox))
            {
                (node, _) = (parentNode, parentElement);
            }
            else
            {
                break;
            }
        }

        // TODO: Should probably choose node at mouseup, because user intention isn't clear at mouse down
        //  i.e. about to drag selection or choose a different item
        var selectedNodes = SelectedItems ??
                            throw new InvalidOperationException("Expected ViewModel to not be null");

        if (canvasSelection.Bounds.Contains(normalizedPosition) &&
            (node.NodeType == HocrNodeType.Page || selectedNodes.Contains(node)))
        {
            // Dragging the selection, no need to select anything else.
            offsetStart = transformation.MapPoint(canvasSelection.Bounds.Location);

            return;
        }


        // Page is unselectable.
        if (node.NodeType == HocrNodeType.Page)
        {
            ClearSelection();

            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
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

        UpdateCanvasSelection();

        offsetStart = transformation.MapPoint(canvasSelection.Bounds.Location);

        dragLimit = CalculateDragLimitBounds(selectedNodes);
    }

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
        transformation =
            transformation.PreConcat(matrix);
        inverseTransformation = transformation.Invert();

        scaleTransformation.ScaleX = transformation.ScaleX;
        scaleTransformation.ScaleY = transformation.ScaleY;
        inverseScaleTransformation.ScaleX = inverseTransformation.ScaleX;
        inverseScaleTransformation.ScaleY = inverseTransformation.ScaleY;
    }

    private void CenterTransformation()
    {
        Debug.Assert(ItemsSource != null, $"{nameof(ItemsSource)} != null");

        var documentBounds = ItemsSource.First(n => n.IsRoot).BBox.ToSKRect();

        var controlSize = SKRect.Create(RenderSize.ToSKSize());

        controlSize.Inflate(CenterPadding);

        var fitBounds = controlSize.AspectFit(documentBounds.Size);

        var resizeFactor = Math.Min(
            fitBounds.Width / documentBounds.Width,
            fitBounds.Height / documentBounds.Height
        );

        ResetTransformation();

        UpdateTransformation(
            SKMatrix.CreateScaleTranslation(
                resizeFactor,
                resizeFactor,
                fitBounds.Left,
                fitBounds.Top
            )
        );
    }

    private void DrawScalingHandle(SKCanvas canvas, ResizeHandle handle)
    {
        var rect = handle.GetRect(transformation);

        canvas.DrawRect(
            rect,
            HandleFillPaint
        );
        canvas.DrawRect(
            rect,
            HandleStrokePaint
        );
    }

    private void Refresh()
    {
        Dispatcher.InvokeAsync(Surface.InvalidateVisual, DispatcherPriority.Render);
    }

    private void BuildDocumentElements(IEnumerable<HocrNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            var el = elementPool.Get();
            el.Bounds = node.BBox.ToSKRect();

            elements.Add(node.Id, (node, el));

            if (node.HocrNode is HocrPage page)
            {
                rootId = node.Id;

                el.Background = SKBitmap.Decode(page.Image);
            }
        }
    }

    private void Canvas_OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.LightGray);

        RenderNodes(canvas);
    }

    private void RenderNodes(SKCanvas canvas)
    {
        if (rootId < 0)
        {
            return;
        }

        using var shaper = new SKShaper(SKTypeface.Default);
        using var paint = new SKPaint(new SKFont(SKTypeface.Default));

        var rootNode = RootNode;

        var page = (HocrPage)rootNode.HocrNode;

        const float fontInchRatio = 1.0f / 72.0f;

        void Recurse(int key)
        {
            var (node, element) = elements[key];

            var bounds = transformation.MapRect(element.Bounds);

            var shouldRenderNode = nodeVisibilityDictionary[node.NodeType];

            if (element.Background != null)
            {
                if (shouldRenderNode)
                {
                    canvas.DrawBitmap(element.Background, bounds);
                }
                else
                {
                    paint.IsStroke = false;
                    paint.Color = SKColors.White;

                    canvas.DrawRect(bounds, paint);
                }

                paint.Color = SKColors.Gray;
                paint.IsStroke = true;
                paint.StrokeWidth = 1;

                canvas.DrawRect(bounds, paint);
            }
            else
            {
                if (shouldRenderNode)
                {
                    var color = GetNodeColor(node);

                    if (node.NodeType == HocrNodeType.Word && IsShowText)
                    {
                        paint.IsStroke = false;
                        paint.Color = SKColors.White.WithAlpha(128);

                        canvas.DrawRect(bounds, paint);

                        var fontSize = ((HocrWord)node.HocrNode).FontSize;

                        paint.TextSize = fontSize * fontInchRatio * page.Dpi.Item2 * transformation.ScaleY * 0.75f;

                        paint.Color = SKColors.Black;
                        paint.IsStroke = false;

                        var textBounds = SKRect.Empty;

                        paint.MeasureText(node.InnerText, ref textBounds);

                        canvas.DrawShapedText(
                            shaper,
                            node.InnerText,
                            bounds.MidX - textBounds.MidX,
                            bounds.MidY - textBounds.MidY,
                            paint
                        );
                    }
                    else
                    {
                        paint.IsStroke = false;
                        paint.Color = node.IsSelected ? SKColors.Red.WithAlpha(16) : color.WithAlpha(16);

                        canvas.DrawRect(bounds, paint);
                    }

                    paint.Color = node.IsSelected ? SKColors.Red : color;
                    paint.IsStroke = true;
                    paint.StrokeWidth = 1;

                    canvas.DrawRect(bounds, paint);
                }
            }

            foreach (var childKey in node.Children.Select(c => c.Id))
            {
                Recurse(childKey);
            }
        }

        Recurse(rootId);

        RenderCanvasSelection(canvas);
    }

    private void RenderCanvasSelection(SKCanvas canvas)
    {
        if (canvasSelection.IsEmpty)
        {
            return;
        }

        var bbox = transformation.MapRect(canvasSelection.Bounds);

        canvas.DrawRect(
            bbox,
            new SKPaint
            {
                IsStroke = true,
                Color = IsFocused ? SKColors.Gray : SKColors.DarkGray,
                StrokeWidth = 1,
            }
        );

        foreach (var handle in canvasSelection.ResizeHandles)
        {
            DrawScalingHandle(canvas, handle);
        }
    }

    private void PerformResize(SKPoint delta)
    {
        var newLocation = offsetStart + delta;

        Debug.Assert(selectedResizeHandle != null, $"{nameof(selectedResizeHandle)} != null");

        var resizePivot = canvasSelection.Center;

        // If more than one element selected, or exactly one element selected _and_ Ctrl is pressed, resize together with children.
        var resizeWithChildren = SelectedItems?.Count > 1 ||
                                 (Keyboard.Modifiers & ModifierKeys.Control) != 0;

        if ((selectedResizeHandle.Direction & CardinalDirections.West) != 0)
        {
            canvasSelection.Left = Math.Clamp(
                newLocation.X,
                resizeLimitOutside.Left,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Right
                    : resizeLimitInside.Left
            );

            resizePivot.X = canvasSelection.Right;
        }

        if ((selectedResizeHandle.Direction & CardinalDirections.North) != 0)
        {
            canvasSelection.Top = Math.Clamp(
                newLocation.Y,
                resizeLimitOutside.Top,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Bottom
                    : resizeLimitInside.Top
            );

            resizePivot.Y = canvasSelection.Bottom;
        }

        if ((selectedResizeHandle.Direction & CardinalDirections.East) != 0)
        {
            canvasSelection.Right = Math.Clamp(
                newLocation.X,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Left
                    : resizeLimitInside.Right,
                resizeLimitOutside.Right
            );

            resizePivot.X = canvasSelection.Left;
        }

        if ((selectedResizeHandle.Direction & CardinalDirections.South) != 0)
        {
            canvasSelection.Bottom = Math.Clamp(
                newLocation.Y,
                resizeLimitInside.IsEmpty || resizeWithChildren
                    ? resizeLimitOutside.Top
                    : resizeLimitInside.Bottom,
                resizeLimitOutside.Bottom
            );

            resizePivot.Y = canvasSelection.Top;
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

    private int GetElementKeyAtPoint(SKPoint p)
    {
        var selectedKeys = SelectedItems?.Select(n => n.Id).ToHashSet() ?? new HashSet<int>();

        var key = elements.Keys
            .Where(k => !selectedKeys.Contains(k))
            .FirstOrDefault(k => elements[k].Item2.Bounds.Contains(p), -1);

        return key < 0
            ? -1
            : GetHierarchy(elements[key].Item1).LastOrDefault(k => elements[k].Item2.Bounds.Contains(p));
    }

    private static IEnumerable<int> GetHierarchy(
        HocrNodeViewModel node
    ) =>
        node.Descendents
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

    protected virtual void OnNodesChanged(NodesChangedEventArgs e)
    {
        NodesChanged?.Invoke(this, e);
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

    protected virtual void OnSelectionChanged(SelectionChangedEventArgs e)
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
        if (e.Key is not Key.Return or Key.Escape)
        {
            return;
        }

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

        Dispatcher.InvokeAsync(UpdateTextBox, DispatcherPriority.Render);
    }

    private void UpdateTextBox()
    {
        Ensure.IsNotNull(nameof(editingNode), editingNode);

        var element = elements[editingNode!.Id].Item2;
        var rect = transformation.MapRect(element.Bounds);

        var word = (HocrWord)editingNode.HocrNode;
        var page = (HocrPage?)editingNode.FindParent(HocrNodeType.Page)?.HocrNode;

        Canvas.SetLeft(TextBox, rect.Left);
        Canvas.SetTop(TextBox, rect.Top);
        TextBox.Width = rect.Width;
        TextBox.Height = rect.Height;

        var fontDpiRatio = (page?.Dpi.Item2 ?? 300) / 72f;
        var fontSize = transformation.ScaleX * word.FontSize * fontDpiRatio * 0.6f;

        TextBox.FontSize = fontSize;
        TextBlock.SetLineHeight(TextBox, fontSize);

        TextBox.Focus();
        TextBox.SelectAll();
    }

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
