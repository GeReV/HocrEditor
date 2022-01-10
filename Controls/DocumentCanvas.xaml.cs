﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
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

    private readonly CanvasSelection canvasSelection = new();
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
                            AddSelectedElements(enumerable);
                        }

                        break;
                }

                Refresh();
            }
        );

        ViewModel.Nodes.CollectionChanged += NodesOnCollectionChanged;
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
        // TODO: Handle granular cases.
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

                if (!canvasSelection.IsEmpty)
                {
                    var selectedHandle = canvasSelection.ResizeHandles
                        .FirstOrDefault(handle => handle.GetRect(transformation).Contains(position));

                    if (selectedHandle != null)
                    {
                        mouseMoveState = MouseState.Resizing;

                        canvasSelection.BeginResize();

                        selectedResizeHandle = selectedHandle;

                        offsetStart = selectedHandle.Center;

                        break;
                    }
                }

                var normalizedPosition = inverseTransformation.MapPoint(position);

                var key = GetElementIndexAtPoint(normalizedPosition);

                if (key == null)
                {
                    ClearSelection();
                    break;
                }

                mouseMoveState = MouseState.Dragging;

                if (canvasSelection.Bounds.Contains(normalizedPosition))
                {
                    // Dragging the selection, no need to select anything else.
                    offsetStart = transformation.MapPoint(canvasSelection.Bounds.Location);

                    break;
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
                if (canvasSelection.Bounds.Contains(normalizedPosition) &&
                    (node.HocrNode.NodeType == HocrNodeType.Page || ViewModel.SelectedNodes.Contains(node)))
                {
                    // Dragging the selection, no need to select anything else.
                    offsetStart = transformation.MapPoint(canvasSelection.Bounds.Location);

                    break;
                }

                // Page is unselectable.
                if (node.HocrNode.NodeType == HocrNodeType.Page)
                {
                    ClearSelection();
                    break;
                }

                if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
                {
                    ClearSelection();
                }

                if (ViewModel.SelectedNodes.Contains(node))
                {
                    RemoveSelectedNode(node);
                }
                else
                {
                    AddSelectedNode(node);
                }

                // Close on deselect?
                foreach (var parent in node.Ascendants)
                {
                    parent.IsExpanded = true;
                }

                UpdateCanvasSelection();

                offsetStart = transformation.MapPoint(canvasSelection.Bounds.Location);

                dragLimit = CalculateDragLimitBounds(ViewModel.SelectedNodes);

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

    private void AddSelectedNode(HocrNodeViewModel node)
    {
        ViewModel?.SelectedNodes.Add(node);

        node.IsSelected = true;

        AddSelectedElements(Enumerable.Repeat(node, 1));
    }

    private void RemoveSelectedNode(HocrNodeViewModel node)
    {
        ViewModel?.SelectedNodes.Remove(node);

        node.IsSelected = false;

        RemoveSelectedElements(Enumerable.Repeat(node, 1));
    }

    private void UpdateCanvasSelection()
    {
        var nodes = selectedElements.Select(id => elements[id].Item1);

        canvasSelection.Bounds = NodeHelpers.CalculateUnionRect(nodes).ToSKRect();
    }

    private void ClearSelection()
    {
        ViewModel?.ClearSelection();

        selectedElements.Clear();

        dragLimit = SKRect.Empty;
        canvasSelection.Bounds = SKRect.Empty;
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

                if (mouseMoveState == MouseState.Resizing)
                {
                    canvasSelection.EndResize();
                }

                mouseMoveState = MouseState.None;

                if (selectedElements.Any())
                {
                    foreach (var id in selectedElements)
                    {
                        var (node, element) = elements[id];

                        var bounds = element.Bounds;

                        node.BBox = (Rect)bounds;
                    }

                    dragLimit = CalculateDragLimitBounds(ViewModel.SelectedNodes);
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
                var newLocation = offsetStart + delta;

                Debug.Assert(selectedResizeHandle != null, $"{nameof(selectedResizeHandle)} != null");

                var resizePivot = canvasSelection.Center;

                if ((selectedResizeHandle.Direction & CardinalDirections.West) != 0)
                {
                    canvasSelection.Left = newLocation.X;

                    resizePivot.X = canvasSelection.Right;
                }

                if ((selectedResizeHandle.Direction & CardinalDirections.North) != 0)
                {
                    canvasSelection.Top = newLocation.Y;

                    resizePivot.Y = canvasSelection.Bottom;
                }

                if ((selectedResizeHandle.Direction & CardinalDirections.East) != 0)
                {
                    canvasSelection.Right = newLocation.X;

                    resizePivot.X = canvasSelection.Left;
                }

                if ((selectedResizeHandle.Direction & CardinalDirections.South) != 0)
                {
                    canvasSelection.Bottom = newLocation.Y;

                    resizePivot.Y = canvasSelection.Top;
                }

                var ratio = canvasSelection.ResizeRatio;

                var matrix = SKMatrix.CreateScale(ratio.X, ratio.Y, resizePivot.X, resizePivot.Y);

                // If more than one element selected, or exactly one element selected _and_ Ctrl is pressed, resize together with children.
                var includeChildren = ViewModel.SelectedNodes.Count > 1 ||
                                      (Keyboard.Modifiers & ModifierKeys.Control) != 0;

                foreach (var id in selectedElements)
                {
                    // Start with the initial value, so pressing and releasing Ctrl reverts to original size.
                    var bounds = elements[id].Item1.BBox.ToSKRect();

                    if (includeChildren || ViewModel.SelectedNodes.Any(node => node.Id == id))
                    {
                        bounds = matrix.MapRect(bounds);
                        bounds.Clamp(canvasSelection.Bounds);
                    }

                    elements[id].Item2.Bounds = bounds;
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

        dragLimit = CalculateDragLimitBounds(
            ViewModel?.SelectedNodes ?? Enumerable.Empty<HocrNodeViewModel>()
        );

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

        BuildDocumentElements(ViewModel.Nodes[0]);

        Dispatcher.InvokeAsync(Refresh, DispatcherPriority.Send);
    }

    private void Refresh()
    {
        Canvas.InvalidateVisual();
    }

    private void BuildDocumentElements(HocrNodeViewModel rootNode)
    {
        var nodes = rootNode.Descendents.Prepend(rootNode);

        foreach (var node in nodes)
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

    private string? GetElementIndexAtPoint(SKPoint p)
    {
        var key = elements.Keys.FirstOrDefault(k => elements[k].Item2.Bounds.Contains(p));

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
}
