using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using HocrEditor.Models;
using HocrEditor.Services;
using HocrEditor.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
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

public partial class DocumentCanvas : UserControl
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

    private readonly CanvasSelection selectionRect = new();
    private ResizeHandle? selectedResizeHandle;

    private HocrDocumentViewModel? ViewModel => (HocrDocumentViewModel?)DataContext;

    public DocumentCanvas()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;

        ClipToBounds = true;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(Update, DispatcherPriority.Send);

        if (ViewModel == null)
        {
            return;
        }

        CenterTransformation();

        ViewModel.Nodes.SubscribeItemPropertyChanged(
            (_, _) => Refresh()
        );

        ViewModel.Nodes.CollectionChanged += NodesOnCollectionChanged;
        ViewModel.SelectedNodes.CollectionChanged += SelectedNodesOnCollectionChanged;
    }

    private void SelectedNodesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        void AddSelectedElements(IEnumerable<HocrNodeViewModel> nodes)
        {
            foreach (var node in nodes)
            {
                if (selectedElements.Contains(node.HocrNode.Id))
                {
                    continue;
                }

                foreach (var id in GetHierarchy(node.HocrNode.Id, elements))
                {
                    selectedElements.Add(id);
                }
            }
        }

        void RemoveSelectedElements(IEnumerable<HocrNodeViewModel> nodes)
        {
            foreach (var node in nodes)
            {
                if (!selectedElements.Contains(node.HocrNode.Id))
                {
                    continue;
                }

                foreach (var id in GetHierarchy(node.HocrNode.Id, elements))
                {
                    selectedElements.Remove(id);
                }
            }
        }

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                // Optimization: If element is included in set, then all its children also are. No need to add them again.
                if (e.NewItems != null)
                {
                    AddSelectedElements(e.NewItems.Cast<HocrNodeViewModel>());
                }

                break;
            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems != null)
                {
                    RemoveSelectedElements(e.OldItems.Cast<HocrNodeViewModel>());
                }

                break;
            case NotifyCollectionChangedAction.Replace:
                if (e.NewItems != null && e.OldItems != null)
                {
                    RemoveSelectedElements(e.OldItems.Cast<HocrNodeViewModel>());
                    AddSelectedElements(e.NewItems.Cast<HocrNodeViewModel>());
                }

                break;
            case NotifyCollectionChangedAction.Move:
                break;
            case NotifyCollectionChangedAction.Reset:
                selectedElements.Clear();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        selectionRect.Rect = CalculateUnionRect(selectedElements, elements);

        Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);
    }

    private void NodesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(Update, DispatcherPriority.Send);
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

                var normalizedPosition = inverseTransformation.MapPoint(position);

                if (!selectionRect.IsEmpty)
                {
                    var selectedHandle = selectionRect.ResizeHandles
                        .FirstOrDefault(handle => handle.GetRect(transformation).Contains(normalizedPosition));

                    if (selectedHandle != null)
                    {
                        mouseMoveState = MouseState.Resizing;

                        selectedResizeHandle = selectedHandle;

                        offsetStart = selectedHandle.Center;

                        break;
                    }
                }

                var key = GetElementIndexAtPoint(normalizedPosition);

                if (key != null && key != rootId)
                {
                    mouseMoveState = MouseState.Dragging;

                    var (node, element) = elements[key];

                    while (node.ParentId != null)
                    {
                        var (parentNode, parentElement) = elements[node.ParentId];

                        if (node.BBox.Equals(parentNode.BBox))
                        {
                            (node, element) = (parentNode, parentElement);
                        }
                        else
                        {
                            break;
                        }
                    }

                    // Page is unselectable.
                    if (node.HocrNode.NodeType != HocrNodeType.Page)
                    {
                        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                        {
                            ViewModel.ClearSelection();
                        }

                        ViewModel.SelectedNodes.Add(node);

                        node.IsSelected = true;

                        if (node.ParentId != null)
                        {
                            var parentNode = elements[node.ParentId].Item1;

                            var parentBounds = parentNode.BBox.ToSKRect();
                            var nodeBounds = node.BBox.ToSKRect();

                            // In some cases, the child node isn't contained within its parent. In that case, don't limit dragging for it (set limit to empty).
                            if (parentBounds.Contains(nodeBounds))
                            {
                                dragLimit = new SKRect(
                                    parentBounds.Left - nodeBounds.Left,
                                    parentBounds.Top - nodeBounds.Top,
                                    parentBounds.Right - nodeBounds.Right,
                                    parentBounds.Bottom - nodeBounds.Bottom
                                );
                            }
                            else
                            {
                                dragLimit = SKRect.Empty;
                            }

                            while (parentNode is { ParentId: { } })
                            {
                                parentNode.IsExpanded = true;
                                parentNode = elements[parentNode.ParentId].Item1;
                            }
                        }

                        offsetStart = transformation.MapPoint(element.Bounds.Location);
                    }
                    else
                    {
                        ViewModel.ClearSelection();
                    }
                }
                else
                {
                    ViewModel.ClearSelection();
                }

                Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);
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
                break;
            case MouseButton.XButton1:
                break;
            case MouseButton.XButton2:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
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

                mouseMoveState = MouseState.None;

                if (selectedElements.Any())
                {
                    foreach (var id in selectedElements)
                    {
                        var (node, element) = elements[id];

                        var bounds = element.Bounds;

                        node.BBox = new BoundingBox(
                            (int)bounds.Left,
                            (int)bounds.Top,
                            (int)bounds.Right,
                            (int)bounds.Bottom
                        );
                    }
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
                if (!selectionRect.IsEmpty)
                {
                    var pt = inverseTransformation.MapPoint(position);

                    var resizeHandles = selectionRect.ResizeHandles;

                    Cursor = null;

                    foreach (var handle in resizeHandles)
                    {
                        if (!handle.GetRect().Contains(pt))
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
                    var node = ViewModel.SelectedNodes.First();

                    var draggedElement = elements[node.HocrNode.Id].Item2;

                    var draggedElementBounds = draggedElement.Bounds;

                    // Apply to all selected elements.
                    foreach (var id in selectedElements)
                    {
                        var (_, element) = elements[id];

                        var deltaFromDraggedElement = element.Bounds.Location - draggedElementBounds.Location;

                        element.Bounds = element.Bounds with
                        {
                            Location = newLocation + deltaFromDraggedElement
                        };
                    }

                    // Apply to self.
                    draggedElement.Bounds = draggedElementBounds with
                    {
                        Location = newLocation
                    };

                    // Apply to selection rect.
                    selectionRect.Rect = selectionRect.Rect with
                    {
                        Location = newLocation
                    };
                }

                Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);

                break;
            }
            case MouseState.Resizing:
            {
                var newLocation = inverseTransformation.MapPoint(offsetStart) + delta;


                Debug.Assert(selectedResizeHandle != null, $"{nameof(selectedResizeHandle)} != null");

                if ((selectedResizeHandle.Direction & CardinalDirections.West) != 0)
                {
                    selectionRect.Left = newLocation.X;
                }

                if ((selectedResizeHandle.Direction & CardinalDirections.North) != 0)
                {
                    selectionRect.Top = newLocation.Y;
                }

                if ((selectedResizeHandle.Direction & CardinalDirections.East) != 0)
                {
                    selectionRect.Right = newLocation.X;
                }

                if ((selectedResizeHandle.Direction & CardinalDirections.South) != 0)
                {
                    selectionRect.Bottom = newLocation.Y;
                }

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

        Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Render);
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

        transformation = SKMatrix.Identity;

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

    private void Update()
    {
        if (ViewModel?.Nodes == null)
        {
            return;
        }

        elements.Clear();

        DrawDocumentNodes(ViewModel.Nodes[0]);

        Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);
    }

    private void Refresh()
    {
        Canvas.InvalidateVisual();
    }

    private void DrawDocumentNodes(HocrNodeViewModel rootNode)
    {
        var traverser = new HierarchyTraverser<HocrNodeViewModel>(node => node.Children);

        foreach (var node in traverser.ToEnumerable(rootNode))
        {
            var el = new Element
            {
                Bounds = node.BBox.ToSKRect()
            };

            elements.Add(node.HocrNode.Id, (node, el));

            if (node.HocrNode is HocrPage page)
            {
                rootId = node.HocrNode.Id;

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

            foreach (var childKey in node.Children.Select(c => c.HocrNode.Id))
            {
                Recurse(childKey);
            }
        }

        if (rootId == null)
        {
            return;
        }

        Recurse(rootId);

        if (selectionRect.IsEmpty)
        {
            return;
        }

        var bbox = transformation.MapRect(selectionRect.Rect);

        canvas.DrawRect(
            bbox,
            new SKPaint
            {
                IsStroke = true,
                Color = SKColors.Gray,
                StrokeWidth = 1
            }
        );

        foreach (var handle in selectionRect.ResizeHandles)
        {
            DrawScalingHandle(canvas, handle);
        }
    }

    private string? GetElementIndexAtPoint(SKPoint p)
    {
        var key = elements.Keys.FirstOrDefault(k => elements[k].Item2.Bounds.Contains(p));

        return key == null
            ? null
            : GetHierarchy(key, elements).LastOrDefault(k => elements[k].Item2.Bounds.Contains(p));
    }

    private static IEnumerable<string> GetHierarchy(
        string rootNodeId,
        IReadOnlyDictionary<string, (HocrNodeViewModel, Element)> elementMap
    ) =>
        new HierarchyTraverser<HocrNodeViewModel>(node => node.Children)
            .ToEnumerable(elementMap[rootNodeId].Item1)
            .Select(node => node.HocrNode.Id)
            .ToList();

    private static SKRect CalculateUnionRect(
        ICollection<string> selection,
        IReadOnlyDictionary<string, (HocrNodeViewModel, Element)> elements
    )
    {
        if (!selection.Any())
        {
            return SKRect.Empty;
        }

        return selection.Skip(1)
            .Aggregate(
                elements[selection.First()].Item2.Bounds,
                (rect, id) =>
                {
                    rect.Union(elements[id].Item2.Bounds);
                    return rect;
                }
            );
    }
}
