using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
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
using Optional;
using Optional.Unsafe;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using Rect = HocrEditor.Models.Rect;
using Size = System.Windows.Size;

namespace HocrEditor.Controls;

public sealed partial class DocumentCanvas
{
    private static readonly SKSize CenterPadding = new(-10.0f, -10.0f);


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
        _ => throw new ArgumentOutOfRangeException(nameof(node))
    };

    private readonly ObjectPool<CanvasElement> elementPool =
        new DefaultObjectPool<CanvasElement>(new DefaultPooledObjectPolicy<CanvasElement>());

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
        typeof(ICanvasTool),
        typeof(DocumentCanvas),
        new FrameworkPropertyMetadata(
            null,
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

    public Option<ObservableHashSet<HocrNodeViewModel>> SelectedItems
    {
        get => ViewModel?.SelectedNodes.Some() ?? Option.None<ObservableHashSet<HocrNodeViewModel>>();
        set
        {
            Ensure.IsNotNull(ViewModel);

            ViewModel.SelectedNodes = value.ValueOrFailure();
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

    public ICanvasTool ActiveTool
    {
        get => (ICanvasTool)GetValue(ActiveToolProperty);
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

    internal int RootId { get; set; } = -1;
    internal Dictionary<int, (HocrNodeViewModel, CanvasElement)> Elements { get; } = new();

    private CancellationTokenSource backgroundLoadCancellationTokenSource = new();
    private SKBitmapManager.SKBitmapReference? background;

    internal CanvasElement RootCanvasElement => Elements[RootId].Item2;

    internal Dictionary<HocrNodeType, bool> NodeVisibilityDictionary = new();

    internal readonly HashSet<int> SelectedElements = new();

    // private List<int> selectedKeyCandidates = new();
    // private int selectedKey = -1;

    private Option<HocrNodeViewModel> editingNode = Option.None<HocrNodeViewModel>();

    public bool IsEditing => editingNode.HasValue;

    internal SKMatrix Transformation { get; private set; } = SKMatrix.Identity;
    internal SKMatrix InverseTransformation { get; private set; } = SKMatrix.Identity;
    internal SKMatrix InverseScaleTransformation { get; private set; } = SKMatrix.Identity;
    private SKMatrix scaleTransformation = SKMatrix.Identity;

    private bool isPanning;

    private SKPoint dragStart;
    private SKPoint offsetStart;

    internal readonly CanvasSelection CanvasSelection = new();
    // private ResizeHandle? selectedResizeHandle;

    private Window? parentWindow;

    public Window ParentWindow => parentWindow ??= Window.GetWindow(this) ?? throw new InvalidOperationException();

    public DocumentCanvas()
    {
        InitializeComponent();

        ClipToBounds = true;

        Unloaded += OnUnloaded;
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)sender;

        documentCanvas.CanvasSelection.Dispose();

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
            case Key.Return when !IsEditing:
            {
                BeginEditing();

                break;
            }
            case Key.Escape:
            {
                ActiveTool = DocumentCanvasTools.SelectionTool;

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
                if (!CanvasSelection.ShouldShowCanvasSelection)
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

        foreach (var (_, element) in documentCanvas.Elements.Values)
        {
            documentCanvas.elementPool.Return(element);
        }

        documentCanvas.ClearCanvasSelection();

        documentCanvas.Elements.Clear();

        documentCanvas.RootId = -1;

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

            documentCanvas.background =
                documentCanvas.IsShowThresholdedImage ? newPage.ThresholdedImage : newPage.Image;

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
                Elements[node.Id].Item2.Bounds = node.BBox.ToSKRectI();
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
            documentCanvas.NodeVisibilityDictionary = new Dictionary<HocrNodeType, bool>(
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
            throw new ArgumentException(
                $"Expected {nameof(sender)} to be of type {nameof(NodeVisibility)}.",
                nameof(sender)
            );
        }

        NodeVisibilityDictionary[nodeVisibility.NodeTypeViewModel.NodeType] = nodeVisibility.Visible;

        Refresh();
    }

    private static void IsShowThresholdedImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var documentCanvas = (DocumentCanvas)d;

        documentCanvas.background =
            (bool)e.NewValue ? documentCanvas.ViewModel?.ThresholdedImage : documentCanvas.ViewModel?.Image;

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

        if (e.OldValue is ICanvasTool previousTool)
        {
            previousTool.Unmount();
        }

        var nextTool = (ICanvasTool)e.NewValue;

        nextTool.Mount(documentCanvas);

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
            if (SelectedElements.Contains(node.Id))
            {
                continue;
            }

            foreach (var id in GetHierarchy(node))
            {
                SelectedElements.Add(id);
            }
        }
    }

    private void RemoveSelectedElements(IEnumerable<HocrNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            if (!SelectedElements.Contains(node.Id))
            {
                continue;
            }

            foreach (var id in GetHierarchy(node))
            {
                SelectedElements.Remove(id);
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
                    var isNewDocument = RootId < 0;

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
                        Elements.Remove(node.Id, out var tuple);
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
                        Elements.Remove(node.Id, out var tuple);
                        elementPool.Return(tuple.Item2);
                    }

                    BuildDocumentElements(list);
                }

                UpdateCanvasSelection();
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Reset:
                foreach (var (_, element) in Elements.Values)
                {
                    elementPool.Return(element);
                }

                Elements.Clear();

                ClearCanvasSelection();
                break;
            default:
                throw new ArgumentOutOfRangeException("e.Action");
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
                throw new ArgumentOutOfRangeException("e.Action");
        }

        ActiveTool = DocumentCanvasTools.SelectionTool;

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

        Mouse.Capture(this);

        var position = e.GetPosition(this).ToSKPoint();

        if (e.ChangedButton != MouseButton.Middle)
        {
            // Noop.
            return;
        }

        e.Handled = true;

        dragStart = position;

        isPanning = true;

        offsetStart = Transformation.MapPoint(SKPoint.Empty);

        Refresh();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        var selectedItems = SelectedItems;
        if (!selectedItems.HasValue)
        {
            return;
        }

        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        if (!isPanning)
        {
            return;
        }

        e.Handled = true;

        isPanning = false;

        ReleaseMouseCapture();

        Refresh();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (!SelectedItems.HasValue)
        {
            return;
        }

        var position = e.GetPosition(this).ToSKPoint();

        var delta = InverseScaleTransformation.MapPoint(position - dragStart);

        if (isPanning)
        {
            e.Handled = true;

            var newLocation = InverseTransformation.MapPoint(offsetStart) + delta;

            UpdateTransformation(SKMatrix.CreateTranslation(newLocation.X, newLocation.Y));
        }

        Refresh();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var delta = Math.Sign(e.Delta) * 3;

        var pointerP = e.GetPosition(this).ToSKPoint();
        var p = InverseTransformation.MapPoint(pointerP);

        var newScale = (float)Math.Pow(2, delta * 0.05f);

        UpdateTransformation(SKMatrix.CreateScale(newScale, newScale, p.X, p.Y));

        Refresh();
    }

    internal void AddSelectedNode(HocrNodeViewModel node)
    {
        OnSelectionChanged(
            new SelectionChangedEventArgs(
                Selector.SelectionChangedEvent,
                Array.Empty<HocrNodeViewModel>(),
                new List<HocrNodeViewModel> { node }
            )
        );
    }

    internal void RemoveSelectedNode(HocrNodeViewModel node)
    {
        OnSelectionChanged(
            new SelectionChangedEventArgs(
                Selector.SelectionChangedEvent,
                new List<HocrNodeViewModel> { node },
                Array.Empty<HocrNodeViewModel>()
            )
        );
    }

    public void ClearSelection() =>
        SelectedItems.MatchSome(
            selectedItems => OnSelectionChanged(
                new SelectionChangedEventArgs(
                    Selector.SelectionChangedEvent,
                    selectedItems.ToList(),
                    Array.Empty<HocrNodeViewModel>()
                )
            )
        );

    internal void ClearCanvasSelection()
    {
        if (RootId < 0)
        {
            return;
        }

        // dragLimit = SKRect.Empty;
        CanvasSelection.Bounds = SKRectI.Empty;

        SelectionBounds = Rect.Empty;

        // ClearCanvasResizeLimit(canvas);

        SelectedElements.Clear();
    }

    internal void UpdateCanvasSelection()
    {
        var allNodes = SelectedElements.Select(id => Elements[id].Item1);

        CanvasSelection.Bounds = NodeHelpers.CalculateUnionRect(allNodes).ToSKRectI();
    }

    private void ResetTransformation()
    {
        Transformation = InverseTransformation = scaleTransformation = InverseScaleTransformation = SKMatrix.Identity;
    }

    private void UpdateTransformation(SKMatrix matrix)
    {
        const float transformationScaleMin = 1 / 32.0f;
        const float transformationScaleMax = 8.0f;

        var nextTransformation = Transformation.PreConcat(matrix);

        // TODO: Clamping to the exact zoom limits is not as straightforward as setting the scale, as the translation
        //  needs to adapt. Figure it out.
        if (nextTransformation.ScaleX < Transformation.ScaleX &&
            nextTransformation.ScaleX < transformationScaleMin)
        {
            return;
        }

        if (nextTransformation.ScaleX > Transformation.ScaleX &&
            nextTransformation.ScaleX > transformationScaleMax)
        {
            return;
        }

        Transformation = nextTransformation;

        InverseTransformation = Transformation.Invert();

        scaleTransformation.ScaleX = Transformation.ScaleX;
        scaleTransformation.ScaleY = Transformation.ScaleY;

        InverseScaleTransformation = InverseScaleTransformation with
        {
            ScaleX = InverseTransformation.ScaleX,
            ScaleY = InverseTransformation.ScaleY
        };
    }

    private void CenterTransformationDocument()
    {
        var documentBounds = RootCanvasElement.Bounds;

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

        var fitBounds = controlSize.AspectFit(CanvasSelection.Size);

        var resizeFactor = Math.Min(
            fitBounds.Width / CanvasSelection.Width,
            fitBounds.Height / CanvasSelection.Height
        );

        resizeFactor = (float)Math.Log(1.0f + resizeFactor) * 0.33f;

        CenterTransformation(CanvasSelection.Bounds, resizeFactor);
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

    public void Refresh()
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
            el.Bounds = node.BBox.ToSKRectI();

            Elements.Add(node.Id, (node, el));

            if (node.HocrNode is HocrPage)
            {
                RootId = node.Id;
            }
        }
    }

    private void Canvas_OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;

        canvas.Clear(SKColors.LightGray);

        RenderCanvas(canvas);
    }

    private static IEnumerable<int> GetHierarchy(
        HocrNodeViewModel node
    ) =>
        node.Descendants
            .Prepend(node)
            .Select(n => n.Id);


    internal void OnNodesChanged(IList<NodesChangedEventArgs.NodeChange> changes)
    {
        RaiseEvent(
            new NodesChangedEventArgs(
                NodesChangedEvent,
                this,
                changes
            )
        );
    }

    internal void OnNodeEdited(string value)
    {
        var enumerable = SelectedItems.Map(set => set.AsEnumerable()).ValueOr(Enumerable.Empty<HocrNodeViewModel>());

        RaiseEvent(
            new NodesEditedEventArgs(
                NodesEditedEvent,
                this,
                SelectionHelper.SelectAllEditable(enumerable),
                value
            )
        );
    }

    internal void OnWordSplit(HocrNodeViewModel node, int splitPosition, (string, string) words)
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

    internal void OnSelectionChanged(SelectionChangedEventArgs e)
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
        var enumerable = SelectedItems.Map(set => set.AsEnumerable())
            .Or(Enumerable.Empty<HocrNodeViewModel>())
            .FlatMap(SelectionHelper.SelectEditable);

        enumerable.MatchSome(
            selectedItem =>
            {
                selectedItem.IsEditing = true;


                var paragraph = (HocrParagraph?)selectedItem.FindParent(HocrNodeType.Paragraph)?.HocrNode;

                TextBox.Text = selectedItem.InnerText;

                editingNode = Option.Some(selectedItem);

                TextBox.Visibility = Visibility.Visible;
                TextBox.FlowDirection =
                    paragraph?.Direction == Direction.Rtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

                UpdateTextBox();
            }
        );
    }

    private void UpdateTextBox() => Dispatcher.BeginInvoke(
        () =>
            editingNode.MatchSome(
                node =>
                {
                    var element = Elements[node.Id].Item2;
                    var rect = Transformation.MapRect(element.Bounds);

                    var word = (HocrWord)node.HocrNode;
                    var line = (HocrLine)Elements[word.ParentId].Item1.HocrNode;

                    Canvas.SetLeft(TextBox, (int)rect.Left);
                    Canvas.SetTop(TextBox, (int)rect.Top);
                    TextBox.Width = rect.Width;
                    TextBox.Height = rect.Height;

                    var fontSize = Transformation.ScaleX * line.FontSize * 0.6f;

                    TextBox.FontSize = fontSize;
                    TextBlock.SetLineHeight(TextBox, fontSize);

                    TextBox.Focus();
                    TextBox.SelectAll();
                }
            ),
        DispatcherPriority.Render
    );

    private void UpdateSelectionPopup() => Dispatcher.BeginInvoke(
        () =>
        {
            var bounds = Transformation.MapRect(CanvasSelection.Bounds);

            SelectionPopup.Visibility =
                CanvasSelection.ShouldShowCanvasSelection
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            Canvas.SetLeft(SelectionPopup, (int)bounds.Left);
            Canvas.SetTop(SelectionPopup, (int)(bounds.Top - SelectionPopup.ActualHeight));
        },
        DispatcherPriority.Render
    );

    public void EndEditing()
    {
        TextBox.Visibility = Visibility.Collapsed;

        editingNode.MatchSome(
            node =>
            {
                node.IsEditing = false;

                editingNode = Option.None<HocrNodeViewModel>();
            }
        );
    }
}
