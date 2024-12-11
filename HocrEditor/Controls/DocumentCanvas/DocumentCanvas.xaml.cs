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
using Rect = HocrEditor.Models.Rect;
using Size = System.Windows.Size;

namespace HocrEditor.Controls;

public sealed partial class DocumentCanvas
{
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
        _ => throw new ArgumentOutOfRangeException(nameof(node)),
    };

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

    internal int RootId { get; private set; } = -1;
    internal Dictionary<int, (HocrNodeViewModel, CanvasElement)> Elements { get; } = new();

    private readonly ObjectPool<CanvasElement> elementPool =
        new DefaultObjectPool<CanvasElement>(new DefaultPooledObjectPolicy<CanvasElement>());

    private CancellationTokenSource backgroundLoadCancellationTokenSource = new();
    private SKBitmapManager.SKBitmapReference? background;

    internal CanvasElement RootCanvasElement => Elements[RootId].Item2;

    internal Dictionary<HocrNodeType, bool> NodeVisibilityDictionary = new();

    internal readonly HashSet<int> SelectedElements = new();

    // private List<int> selectedKeyCandidates = new();
    // private int selectedKey = -1;

    private Option<HocrNodeViewModel> editingNode = Option.None<HocrNodeViewModel>();

    public bool IsEditing => editingNode.HasValue;

    internal readonly CanvasSelection CanvasSelection = new();
    // private ResizeHandle? selectedResizeHandle;

    private Window? parentWindow;

    public Window ParentWindow => parentWindow ??= Window.GetWindow(this) ?? throw new InvalidOperationException();

    public DocumentCanvas()
    {
        InitializeComponent();

        ClipToBounds = true;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        KeyDown += OnKeyDown;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        KeyDown -= OnKeyDown;

        // TODO: Figure out proper disposal event.
        // var documentCanvas = (DocumentCanvas)sender;
        //
        // documentCanvas.CanvasSelection.Dispose();
        //
        // documentCanvas.bidi.Dispose();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
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

                break;
            }
            default:
                return;
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

            documentCanvas.background = newPage.Image;

            newPage.Nodes.SubscribeItemPropertyChanged(documentCanvas.NodesOnItemPropertyChanged);

            newPage.Nodes.CollectionChanged += documentCanvas.NodesOnCollectionChanged;

            newPage.SelectedNodes.CollectionChanged += documentCanvas.SelectedNodesOnCollectionChanged;

            if (newPage.Nodes.Count > 0)
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

        if (e.ChangedButton != MouseButton.Middle)
        {
            // Noop.
            return;
        }

        e.Handled = true;

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

        e.Handled = true;

        ReleaseMouseCapture();

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

    private void CenterTransformationDocument() =>
        Surface.CenterTransformation(RootCanvasElement.Bounds);

    private void CenterTransformationSelection() =>
        Surface.CenterTransformation(CanvasSelection.Bounds);

    public void Refresh()
    {
        _ = Dispatcher.InvokeAsync(Surface.InvalidateVisual, DispatcherPriority.Render);

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

    private void Canvas_OnPaint(object? sender, ZoomPanPaintEventArgs e)
    {
        RenderCanvas(e.Surface.Canvas);
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
        var wasEditing = IsEditing;
        var text = EndEditing();

        if (wasEditing)
        {
            OnNodeEdited(text);
        }
    }

    private void TextBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Return or Key.Escape))
        {
            return;
        }

        e.Handled = true;

        var text = EndEditing();

        if (e.Key == Key.Escape)
        {
            return;
        }

        OnNodeEdited(text);
    }

    private void BeginEditing()
    {
        SelectedItems
            .FlatMap(SelectionHelper.SelectEditable)
            .MatchSome(
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
                    var rect = Surface.Transform.MapRect(element.Bounds);

                    if (node.HocrNode is not HocrWord word)
                    {
                        return;
                    }

                    var line = (HocrLine)Elements[word.ParentId].Item1.HocrNode;

                    Canvas.SetLeft(TextBox, (int)rect.Left);
                    Canvas.SetTop(TextBox, (int)rect.Top);
                    TextBox.Width = rect.Width;
                    TextBox.Height = rect.Height;

                    var fontSize = Surface.Transform.ScaleX * line.FontSize * 0.6f;

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
            var bounds = Surface.Transform.MapRect(CanvasSelection.Bounds);

            SelectionPopup.Visibility =
                CanvasSelection.ShouldShowCanvasSelection
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            Canvas.SetLeft(SelectionPopup, (int)bounds.Left);
            Canvas.SetTop(SelectionPopup, (int)(bounds.Top - SelectionPopup.ActualHeight));
        },
        DispatcherPriority.Render
    );

    public string EndEditing()
    {
        TextBox.Visibility = Visibility.Collapsed;

        editingNode.MatchSome(
            node =>
            {
                node.IsEditing = false;

                editingNode = Option.None<HocrNodeViewModel>();
            }
        );

        return TextBox.Text;
    }
}
