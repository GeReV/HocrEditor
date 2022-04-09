using System.Windows.Input;
using HocrEditor.Helpers;
using Optional.Unsafe;
using SkiaSharp;

namespace HocrEditor.Controls;

public sealed class RegionSelectionTool : RegionToolBase
{
    public override void Mount(DocumentCanvas canvas)
    {
        base.Mount(canvas);

        canvas.Cursor = canvas.CurrentCursor = Cursors.Cross;
    }

    protected override void Unmount(DocumentCanvas canvas)
    {
        canvas.Cursor = canvas.CurrentCursor = null;
    }

    public override void Render(SKCanvas canvas)
    {
        var control = Canvas.ValueOrFailure();

        control.CanvasSelection.Render(
            canvas,
            control.Transformation,
            SKColor.Empty
        );
    }

    protected override void OnMouseDown(DocumentCanvas canvas, MouseButtonEventArgs e, SKPointI normalizedPosition)
    {
        if (canvas.CanvasSelection.ShouldShowCanvasSelection &&
            canvas.CanvasSelection.Bounds.Contains(normalizedPosition))
        {
            // Handle dragging the selection region.
            BeginDrag(canvas);
        }
        else
        {
            MouseMoveState = RegionToolMouseState.Selecting;

            canvas.EndEditing();

            canvas.ClearSelection();

            DragLimit = canvas.RootCanvasElement.Bounds;

            var bounds = SKRect.Create(normalizedPosition, SKSize.Empty);

            bounds.Clamp(DragLimit);

            canvas.CanvasSelection.Bounds = SKRectI.Truncate(bounds);
        }
    }

    protected override bool OnSelectSelection(DocumentCanvas canvas, SKPoint delta)
    {
        var newLocation = SKPointI.Truncate(canvas.InverseTransformation.MapPoint(DragStart) + delta);

        newLocation.Clamp(DragLimit);

        canvas.CanvasSelection.Right = newLocation.X;
        canvas.CanvasSelection.Bottom = newLocation.Y;

        return true;
    }

    protected override bool OnDragSelection(DocumentCanvas canvas, SKPoint delta)
    {
        var newLocation = SKPointI.Truncate(canvas.InverseTransformation.MapPoint(OffsetStart) + delta);

        newLocation.Clamp(DragLimit);

        var newBounds = canvas.CanvasSelection.Bounds with
        {
            Location = newLocation
        };

        canvas.CanvasSelection.Bounds = newBounds;

        return true;
    }

    protected override SKRectI CalculateDragLimitBounds(DocumentCanvas canvas)
    {
        var parentBounds = canvas.RootCanvasElement.Bounds;

        return SKRectI.Create(
            parentBounds.Width - canvas.CanvasSelection.Bounds.Width,
            parentBounds.Height - canvas.CanvasSelection.Bounds.Height
        );
    }
}
