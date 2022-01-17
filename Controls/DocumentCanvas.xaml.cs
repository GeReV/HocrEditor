using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
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
}

public class Element
{
    public SKBitmap? Background { get; set; }
    public SKRect Bounds { get; set; }

    public SKColor BorderColor { get; set; } = SKColor.Empty;

    public float BorderWidth { get; set; }

    public SKColor FillColor = SKColor.Empty;
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

    private static readonly SKColor[] NodeColors =
    {
        SKColors.Aqua,
        SKColors.LightGreen,
        SKColors.LightYellow,
        SKColors.MistyRose,
        SKColors.MediumPurple
    };

    private string? rootId;
    private readonly Dictionary<string, (HocrNodeViewModel, Element)> elements = new();

    private readonly HashSet<string> selectedElements = new();

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

    private readonly CanvasSelection canvasSelection = new();
    private ResizeHandle? selectedResizeHandle;

    private HocrDocumentViewModel? ViewModel => (HocrDocumentViewModel?)DataContext;


    public event EventHandler<NodesChangedEventArgs>? NodesChanged;

    public event SelectionChangedEventHandler? SelectionChanged;

    public DocumentCanvas()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        ClipToBounds = true;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (ViewModel == null)
        {
            return;
        }

        elements.Clear();

        var rootNode = ViewModel.Nodes[0];

        BuildDocumentElements(rootNode.Descendents.Prepend(rootNode));

        ViewModel.Nodes.SubscribeItemPropertyChanged(
            (nodeSender, nodePropertyChangedArgs) =>
            {
                Debug.Assert(nodeSender != null, $"{nameof(nodeSender)} != null");

                var node = (HocrNodeViewModel)nodeSender;

                switch (nodePropertyChangedArgs.PropertyName)
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
        );

        ViewModel.Nodes.CollectionChanged += NodesOnCollectionChanged;
        ViewModel.SelectedNodes.CollectionChanged += SelectedNodesOnCollectionChanged;

        Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);

        CenterTransformation();
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

                    Debug.WriteLine(string.Join(' ', list.Select(n => n.Id)));

                    RemoveSelectedElements(list);

                    foreach (var node in list)
                    {
                        elements.Remove(node.Id);
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
                        elements.Remove(node.Id);
                    }

                    BuildDocumentElements(list);
                }

                UpdateCanvasSelection();
                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Reset:
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

        Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        if (ViewModel == null)
        {
            return;
        }

        Mouse.Capture(this);

        var position = e.GetPosition(this).ToSKPoint();

        dragStart = position;

        switch (e.ChangedButton)
        {
            case MouseButton.Left:
            {
                e.Handled = true;

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

                mouseMoveState = MouseState.Dragging;

                if (canvasSelection.Bounds.Contains(normalizedPosition))
                {
                    // Dragging the selection, no need to select anything else.
                    offsetStart = transformation.MapPoint(canvasSelection.Bounds.Location);

                    break;
                }

                SelectNode(normalizedPosition);

                break;
            }
            case MouseButton.Middle:
            {
                e.Handled = true;

                mouseMoveState = MouseState.Panning;

                offsetStart = transformation.MapPoint(SKPoint.Empty);

                break;
            }
            case MouseButton.Right:
            case MouseButton.XButton1:
            case MouseButton.XButton2:
            default:
                // Noop.
                break;
        }

        Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);
    }

    private void SelectNode(SKPoint normalizedPosition)
    {
        var key = GetElementKeyAtPoint(normalizedPosition);

        if (key == null)
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
        var selectedNodes = ViewModel?.SelectedNodes ??
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
        if (ViewModel != null && ViewModel.SelectedNodes.Count == 1)
        {
            var node = ViewModel.SelectedNodes[0];

            var containedChildren = node.Children.Where(c => node.BBox.Contains(c.BBox));

            resizeLimitInside = NodeHelpers.CalculateUnionRect(containedChildren).ToSKRect();

            Debug.Assert(
                resizeLimitInside.IsEmpty || canvasSelection.Bounds.Contains(resizeLimitInside),
                "Expected inner resize limit to be contained in the canvas selection bounds."
            );

            if (node.ParentId != null)
            {
                resizeLimitOutside = elements[node.ParentId].Item2.Bounds;

                Debug.Assert(
                    resizeLimitOutside.Contains(canvasSelection.Bounds),
                    "Expected outer resize limit to contain the canvas selection bounds."
                );
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

        ClearCanvasResizeLimit();

        selectedElements.Clear();
    }

    private void ClearCanvasResizeLimit()
    {
        resizeLimitInside = SKRect.Empty;
        resizeLimitOutside = elements[rootId ?? throw new ArgumentNullException(nameof(rootId))].Item2.Bounds;
    }

    private void ClearSelection()
    {
        if (ViewModel != null)
        {
            OnSelectionChanged(
                new SelectionChangedEventArgs(
                    Selector.SelectionChangedEvent,
                    ViewModel.SelectedNodes.ToList(),
                    Array.Empty<HocrNodeViewModel>()
                )
            );
        }

        ClearCanvasSelection();
    }


    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        if (ViewModel == null)
        {
            return;
        }

        switch (e.ChangedButton)
        {
            case MouseButton.Middle:
            {
                e.Handled = true;

                mouseMoveState = MouseState.None;
                break;
            }
            case MouseButton.Left:
            {
                e.Handled = true;

                var position = e.GetPosition(this).ToSKPoint();

                if (mouseMoveState == MouseState.Resizing)
                {
                    canvasSelection.EndResize();

                    ReleaseKeyDownEvents();
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

                    dragLimit = CalculateDragLimitBounds(ViewModel.SelectedNodes);
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
                break;
        }

        ReleaseMouseCapture();

        Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (ViewModel == null)
        {
            return;
        }

        var position = e.GetPosition(this).ToSKPoint();

        var delta = inverseScaleTransformation.MapPoint(position - dragStart);

        switch (mouseMoveState)
        {
            case MouseState.None:
                if (!canvasSelection.IsEmpty)
                {
                    var resizeHandles = canvasSelection.ResizeHandles;

                    Cursor = null;

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

                        break;
                    }
                }

                break;
            case MouseState.Panning:
            {
                e.Handled = true;

                var newLocation = inverseTransformation.MapPoint(offsetStart) + delta;

                UpdateTransformation(SKMatrix.CreateTranslation(newLocation.X, newLocation.Y));

                Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);

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

                if (ViewModel.SelectedNodes.Any())
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

                Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);

                break;
            }
            case MouseState.Resizing:
            {
                PerformResize(delta);

                Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);

                break;
            }
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var delta = Math.Sign(e.Delta) * 3;

        var pointerP = e.GetPosition(this).ToSKPoint();
        var p = inverseTransformation.MapPoint(pointerP);

        var newScale = (float)Math.Pow(2, delta * 0.05);

        UpdateTransformation(SKMatrix.CreateScale(newScale, newScale, p.X, p.Y));

        dragLimit = CalculateDragLimitBounds(
            ViewModel?.SelectedNodes ?? Enumerable.Empty<HocrNodeViewModel>()
        );

        Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Render);
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
        Debug.Assert(ViewModel != null, $"{nameof(ViewModel)} != null");

        var documentBounds = ViewModel.Nodes.First(n => n.IsRoot).BBox.ToSKRect();

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
        Canvas.InvalidateVisual();
    }

    private void BuildDocumentElements(IEnumerable<HocrNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            var el = new Element
            {
                Bounds = node.BBox.ToSKRect()
            };

            elements.Add(node.Id, (node, el));

            if (node.HocrNode is HocrPage page)
            {
                rootId = node.Id;

                el.Background = SKBitmap.Decode(page.Image);
            }
            else
            {
                var color = NodeColors[0];

                el.BorderColor = color;
                el.BorderWidth = 1;
                el.FillColor = color.WithAlpha(16);
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
        void Recurse(string key)
        {
            var (node, element) = elements[key];

            var paint = new SKPaint();

            var bounds = transformation.MapRect(element.Bounds);

            if (element.Background != null)
            {
                canvas.DrawBitmap(element.Background, bounds);

                paint.Color = SKColors.Gray;
                paint.IsStroke = true;
                paint.StrokeWidth = element.BorderWidth;

                canvas.DrawRect(bounds, paint);
            }
            else
            {
                paint.Color = node.IsSelected ? SKColors.Red : element.BorderColor;
                paint.IsStroke = true;
                paint.StrokeWidth = element.BorderWidth;

                canvas.DrawRect(bounds, paint);

                paint.IsStroke = false;
                paint.Color = node.IsSelected ? SKColors.Red.WithAlpha(16) : element.FillColor;

                canvas.DrawRect(bounds, paint);
            }

            foreach (var childKey in node.Children.Select(c => c.Id))
            {
                Recurse(childKey);
            }
        }

        if (rootId == null)
        {
            return;
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
                Color = SKColors.Gray,
                StrokeWidth = 1
            }
        );

        foreach (var handle in canvasSelection.ResizeHandles)
        {
            DrawScalingHandle(canvas, handle);
        }
    }

    private void PerformResize(SKPoint delta)
    {
        if (ViewModel == null)
        {
            return;
        }

        var newLocation = offsetStart + delta;

        Debug.Assert(selectedResizeHandle != null, $"{nameof(selectedResizeHandle)} != null");

        var resizePivot = canvasSelection.Center;

        // If more than one element selected, or exactly one element selected _and_ Ctrl is pressed, resize together with children.
        var resizeWithChildren = ViewModel.SelectedNodes.Count > 1 ||
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

            if (resizeWithChildren || ViewModel.SelectedNodes.Any(node => node.Id == id))
            {
                bounds = matrix.MapRect(bounds);
                bounds.Clamp(canvasSelection.Bounds);
            }

            elements[id].Item2.Bounds = bounds;
        }
    }

    private string? GetElementKeyAtPoint(SKPoint p)
    {
        var selectedKeys = ViewModel?.SelectedNodes.Select(n => n.Id).ToHashSet() ?? Enumerable.Empty<string>();

        var key = elements.Keys
            .Where(k => !selectedKeys.Contains(k))
            .FirstOrDefault(k => elements[k].Item2.Bounds.Contains(p));

        return key == null
            ? null
            : GetHierarchy(elements[key].Item1).LastOrDefault(k => elements[k].Item2.Bounds.Contains(p));
    }

    private static IEnumerable<string> GetHierarchy(
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
        var window = Window.GetWindow(this);

        Debug.Assert(window != null, $"{nameof(window)} != null");

        window.KeyDown += WindowOnKeyChange;
        window.KeyUp += WindowOnKeyChange;
    }

    private void ReleaseKeyDownEvents()
    {
        var window = Window.GetWindow(this);

        Debug.Assert(window != null, $"{nameof(window)} != null");

        window.KeyDown -= WindowOnKeyChange;
        window.KeyUp -= WindowOnKeyChange;
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

        Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);
    }

    protected virtual void OnNodesChanged(NodesChangedEventArgs e)
    {
        NodesChanged?.Invoke(this, e);
    }

    protected virtual void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        SelectionChanged?.Invoke(this, e);
    }
}
