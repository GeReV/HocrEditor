using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using Optional;
using Optional.Unsafe;
using SkiaSharp;
using SkiaSharp.Views.WPF;
using Rect = HocrEditor.Models.Rect;

namespace HocrEditor.Controls;

public sealed class SelectionTool : RegionToolBase
{
    private static readonly SKColor NodeSelectionColor = new(0xffa6cee3);
    private static readonly SKColor NodeSelectorColor = new(0xff1f78b4);

    private SKRect nodeSelection = SKRect.Empty;

    // Debounce selection on mouse up by this duration to prevent a bounce
    // (double sequential selection, on mousedown then on mouseup immediately).
    private bool selectionChangedLatch;

    private List<int> selectedKeyCandidates = new();
    private int selectedKey = -1;

    private Option<ObservableHashSet<HocrNodeViewModel>> selectedItems =
        Option.None<ObservableHashSet<HocrNodeViewModel>>();

    private static DependencyPropertyDescriptor ViewModelProperty => DependencyPropertyDescriptor.FromProperty(
        DocumentCanvas.ViewModelProperty,
        typeof(DocumentCanvas)
    );

    public override void Mount(DocumentCanvas canvas)
    {
        base.Mount(canvas);

        ViewModelProperty
            .AddValueChanged(canvas, OnDocumentCanvasViewModelChanged);

        // Transfer the current selection when we turn on the tool.
        if (!canvas.CanvasSelection.IsEmpty)
        {
            canvas.SelectionBounds = Rect.FromSKRect(canvas.CanvasSelection.Bounds);
        }
    }

    protected override void Unmount(DocumentCanvas canvas)
    {
        ViewModelProperty
            .RemoveValueChanged(canvas, OnDocumentCanvasViewModelChanged);

        selectedItems.MatchSome(
            items => items.CollectionChanged -= SelectedItemsOnCollectionChanged
        );
        selectedItems = Option.None<ObservableHashSet<HocrNodeViewModel>>();
    }

    public override void Render(SKCanvas canvas)
    {
        var control = Canvas.ValueOrFailure();

        control.CanvasSelection.Render(
            canvas,
            control.Transformation,
            NodeSelectionColor
        );

        if (nodeSelection.IsEmpty)
        {
            return;
        }


        var bbox = control.Transformation.MapRect(nodeSelection);

        var paint = new SKPaint
        {
            IsStroke = false,
            Color = NodeSelectorColor.WithAlpha(64),
        };

        canvas.DrawRect(bbox, paint);

        paint.IsStroke = true;
        paint.Color = NodeSelectorColor;

        canvas.DrawRect(bbox, paint);
    }

    protected override void OnMouseDown(DocumentCanvas canvas, MouseButtonEventArgs e, SKPointI normalizedPosition)
    {
        // Dragging current the selection, no need to select anything else.
        if (canvas.CanvasSelection.ShouldShowCanvasSelection &&
            canvas.CanvasSelection.Bounds.Contains(normalizedPosition))
        {
            BeginDrag(canvas);

            canvas.Refresh();

            return;
        }

        SelectNode(canvas, normalizedPosition);

        // Dragging the new selection, no need to select anything else.
        if (canvas.CanvasSelection.ShouldShowCanvasSelection &&
            canvas.CanvasSelection.Bounds.Contains(normalizedPosition))
        {
            BeginDrag(canvas);

            canvas.Refresh();

            return;
        }

        if (!canvas.SelectedElements.Any() && canvas.RootCanvasElement.Bounds.Contains(normalizedPosition))
        {
            MouseMoveState = RegionToolMouseState.Selecting;

            DragLimit = canvas.RootCanvasElement.Bounds;

            var bounds = SKRect.Create(normalizedPosition, SKSize.Empty);

            bounds.Clamp(DragLimit);

            nodeSelection = bounds;

            SelectNodesWithinRegion(canvas, bounds);
        }
    }

    protected override void OnMouseUp(DocumentCanvas canvas, MouseButtonEventArgs e, SKPoint normalizedPosition)
    {
        canvas.SelectedItems.MatchSome(
            items =>
            {
                if (e.ChangedButton != MouseButton.Left)
                {
                    return;
                }

                e.Handled = true;

                var mouseMoved = e.GetPosition(canvas).ToSKPoint() != DragStart;

                switch (MouseMoveState)
                {
                    case RegionToolMouseState.None:
                    case RegionToolMouseState.Dragging:
                    {
                        if (!mouseMoved)
                        {
                            SelectNode(canvas, normalizedPosition);
                        }

                        break;
                    }
                    case RegionToolMouseState.Selecting:
                    {
                        nodeSelection = SKRect.Empty;

                        break;
                    }
                    case RegionToolMouseState.Resizing:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(MouseMoveState));
                }

                if (canvas.SelectedElements.Any() && mouseMoved)
                {
                    UpdateNodes(canvas);

                    DragLimit = NodeHelpers.CalculateDragLimitBounds(items);
                }

                // Reset selection latch when a click cycle (mousedown-mouseup) is complete.
                selectionChangedLatch = false;
            }
        );
    }

    protected override bool OnSelectSelection(DocumentCanvas canvas, SKPoint delta) =>
        canvas.SelectedItems.Map(
                items =>
                {
                    var newLocation = canvas.InverseTransformation.MapPoint(DragStart) + delta;

                    newLocation.Clamp(DragLimit);

                    nodeSelection.Right = newLocation.X;
                    nodeSelection.Bottom = newLocation.Y;

                    var selection = SelectNodesWithinRegion(canvas, nodeSelection);

                    IList removed = Array.Empty<HocrNodeViewModel>();

                    if (items is { Count: > 0 })
                    {
                        removed = items.Except(selection).ToList();

                        selection.ExceptWith(items);
                    }

                    canvas.OnSelectionChanged(
                        new SelectionChangedEventArgs(Selector.SelectionChangedEvent, removed, selection.ToList())
                    );

                    return true;
                }
            )
            .ValueOr(false);

    protected override bool OnDragSelection(DocumentCanvas canvas, SKPoint delta) =>
        canvas.SelectedItems.Map(
                items =>
                {
                    if (!DragLimit.IsEmpty)
                    {
                        delta.Clamp(DragLimit);
                    }

                    var newLocation = canvas.InverseTransformation.MapPoint(OffsetStart) + delta;

                    if (items.Any())
                    {
                        // Apply to all selected elements.
                        foreach (var id in canvas.SelectedElements)
                        {
                            var (_, element) = canvas.Elements[id];

                            var deltaFromDraggedElement =
                                element.Bounds.Location - canvas.CanvasSelection.Bounds.Location;

                            element.Bounds = element.Bounds with
                            {
                                Location = SKPointI.Truncate(newLocation + deltaFromDraggedElement)
                            };
                        }

                        // Apply to selection rect.
                        canvas.CanvasSelection.Bounds = canvas.CanvasSelection.Bounds with
                        {
                            Location = SKPointI.Truncate(newLocation)
                        };
                    }

                    return true;
                }
            )
            .ValueOr(false);

    protected override SKRectI CalculateDragLimitBounds(DocumentCanvas canvas) =>
        NodeHelpers.CalculateDragLimitBounds(canvas.SelectedItems.ValueOrFailure());

    private void OnDocumentCanvasViewModelChanged(object? sender, EventArgs e)
    {
        ArgumentNullException.ThrowIfNull(sender);

        var canvas = (DocumentCanvas)sender;

        selectedItems.MatchSome(
            items => items.CollectionChanged -= SelectedItemsOnCollectionChanged
        );

        selectedItems = canvas.SelectedItems;
        selectedItems.MatchSome(items => items.CollectionChanged += SelectedItemsOnCollectionChanged);
    }

    private void SelectedItemsOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        var canvas = Canvas.ValueOrFailure();

        if (canvas.SelectedItems.Exists(items => items.Count > 0))
        {
            ResetSelectionCycle();
        }
    }

    private static HashSet<HocrNodeViewModel> SelectNodesWithinRegion(DocumentCanvas canvas, SKRect selection)
    {
        if (canvas.RootId < 0)
        {
            throw new InvalidOperationException($"Expected {canvas.RootId} to be greater or equal to 0.");
        }

        selection = canvas.Transformation.MapRect(selection);

        var selectedNodes = new HashSet<HocrNodeViewModel>();

        void Recurse(int key)
        {
            var (node, element) = canvas.Elements[key];

            var bounds = canvas.Transformation.MapRect(element.Bounds);

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

        Recurse(canvas.RootId);

        return selectedNodes;
    }

    private void ResetSelectionCycle()
    {
        selectedKey = -1;
        selectedKeyCandidates.Clear();
    }

    private void SelectNode(DocumentCanvas canvas, SKPoint normalizedPosition)
    {
        if (selectionChangedLatch)
        {
            return;
        }

        // Get keys for all nodes overlapping at this point.
        var newKeyCandidates = GetVisibleElementKeysAtPoint(canvas, normalizedPosition);

        if (!newKeyCandidates.Any())
        {
            canvas.ClearSelection();

            canvas.ClearCanvasSelection();

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

            selectedKey = PickFirstSelectionCandidate(canvas, selectedKeyCandidates);
        }

        var node = canvas.Elements[selectedKey].Item1;

        //  i.e. about to drag selection or choose a different item
        var selectedNodes = canvas.SelectedItems.ValueOrFailure();

        // Page is unselectable.
        if (node.NodeType == HocrNodeType.Page)
        {
            canvas.ClearSelection();

            return;
        }

        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            canvas.ClearSelection();
        }

        if (selectedNodes.Contains(node))
        {
            canvas.RemoveSelectedNode(node);
        }
        else
        {
            canvas.AddSelectedNode(node);
        }

        selectionChangedLatch = true;

        canvas.UpdateCanvasSelection();
    }

    private static List<int> GetVisibleElementKeysAtPoint(DocumentCanvas canvas, SKPoint p)
    {
        bool NodeIsVisible(int key)
        {
            var node = canvas.Elements[key].Item1;

            var visible = canvas.NodeVisibilityDictionary[node.NodeType];

            return visible;
        }

        return canvas.Elements.Keys
            .Where(NodeIsVisible)
            .Where(k => canvas.Elements[k].Item1.NodeType != HocrNodeType.Page)
            .Where(k => canvas.Elements[k].Item2.Bounds.Contains(SKPointI.Truncate(p)))
            .OrderByDescending(k => canvas.Elements[k].Item1.NodeType)
            .ToList();
    }

    // Pick the node with the smallest area, as it's likely the most specific one.
    //  (e.g. word rather than line or paragraph)
    private static int PickFirstSelectionCandidate(DocumentCanvas canvas, IEnumerable<int> candidates) =>
        candidates.MinBy(
            k =>
            {
                var node = canvas.Elements[k].Item1;

                return node.BBox.Width * node.BBox.Height;
            }
        );
}
