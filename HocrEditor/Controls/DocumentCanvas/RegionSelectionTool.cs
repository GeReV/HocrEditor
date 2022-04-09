using System;
using System.Windows.Input;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using Optional;
using Optional.Unsafe;
using SkiaSharp;
using SkiaSharp.Views.WPF;

namespace HocrEditor.Controls;

public class RegionSelectionTool : RegionToolBase
{
    public bool CanMount(HocrPageViewModel page) => true;

    public override void Mount(DocumentCanvas canvas)
    {
        base.Mount(canvas);

        canvas.Cursor = canvas.CurrentCursor = Cursors.Cross;
    }

    public override void Unmount()
    {
        base.Unmount();

        var canvas = Canvas.ValueOrFailure();

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

    protected override void OnMouseDown(DocumentCanvas canvas, MouseButtonEventArgs e, SKPoint normalizedPosition)
    {
        if (canvas.CanvasSelection.ShouldShowCanvasSelection &&
            canvas.CanvasSelection.Bounds.Contains(normalizedPosition))
        {
            // Handle dragging the selection region.
            MouseMoveState = RegionToolMouseState.Dragging;

            var parentBounds = canvas.RootCanvasElement.Bounds;

            DragLimit = SKRect.Create(
                parentBounds.Width - canvas.CanvasSelection.Bounds.Width,
                parentBounds.Height - canvas.CanvasSelection.Bounds.Height
            );

            OffsetStart = canvas.Transformation.MapPoint(canvas.CanvasSelection.Bounds.Location);
        }
        else
        {
            MouseMoveState = RegionToolMouseState.Selecting;

            canvas.EndEditing();

            canvas.ClearSelection();

            DragLimit = canvas.RootCanvasElement.Bounds;

            var bounds = SKRect.Create(normalizedPosition, SKSize.Empty);

            bounds.Clamp(DragLimit);

            canvas.CanvasSelection.Bounds = bounds;
        }
    }

    protected override void OnMouseMove(DocumentCanvas canvas, MouseEventArgs e, SKPoint delta)
    {
        switch (MouseMoveState)
        {
            case RegionToolMouseState.Selecting:
            {
                var newLocation = canvas.InverseTransformation.MapPoint(DragStart) + delta;

                newLocation.Clamp(DragLimit);

                canvas.CanvasSelection.Right = newLocation.X;
                canvas.CanvasSelection.Bottom = newLocation.Y;

                break;
            }
            case RegionToolMouseState.Dragging:
            {
                e.Handled = true;

                var newLocation = canvas.InverseTransformation.MapPoint(OffsetStart) + delta;

                newLocation.Clamp(DragLimit);

                var newBounds = canvas.CanvasSelection.Bounds with
                {
                    Location = newLocation
                };

                canvas.CanvasSelection.Bounds = newBounds;

                break;
            }
            case RegionToolMouseState.None:
            case RegionToolMouseState.Resizing:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(MouseMoveState));
        }
    }
}
