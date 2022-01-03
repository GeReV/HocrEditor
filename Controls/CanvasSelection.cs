using System.Collections.Generic;
using SkiaSharp;

namespace HocrEditor.Controls;

internal class CanvasSelection
{
    private SKRect rect;
    private readonly ResizeHandle[] resizeHandles;

    public CanvasSelection()
    {
        resizeHandles = new[]
        {
            // Clockwise from top-left.
            new ResizeHandle(Rect.Location, CardinalDirections.NorthWest),
            new ResizeHandle(new SKPoint(Rect.MidX, Rect.Top), CardinalDirections.North),
            new ResizeHandle(new SKPoint(Rect.Right, Rect.Top), CardinalDirections.NorthEast),
            new ResizeHandle(new SKPoint(Rect.Right, Rect.MidY), CardinalDirections.East),
            new ResizeHandle(new SKPoint(Rect.Right, Rect.Bottom), CardinalDirections.SouthEast),
            new ResizeHandle(new SKPoint(Rect.MidX, Rect.Bottom), CardinalDirections.South),
            new ResizeHandle(new SKPoint(Rect.Left, Rect.Bottom), CardinalDirections.SouthWest),
            new ResizeHandle(new SKPoint(Rect.Left, Rect.MidY), CardinalDirections.West),
        };
    }

    public SKRect Rect
    {
        get => rect;
        set => rect = value;
    }

    public IEnumerable<ResizeHandle> ResizeHandles
    {
        get
        {
            CalculateRectResizeHandles(rect);

            return resizeHandles;
        }
    }

    public bool IsEmpty => Rect.IsEmpty;

    public float Left
    {
        get => rect.Left;
        set => rect.Left = value;
    }

    public float Top
    {
        get => rect.Top;
        set => rect.Top = value;
    }

    public float Right
    {
        get => rect.Right;
        set => rect.Right = value;
    }

    public float Bottom
    {
        get => rect.Bottom;
        set => rect.Bottom = value;
    }

    private void CalculateRectResizeHandles(SKRect r)
    {
        resizeHandles[0].Center = r.Location;
        resizeHandles[1].Center = new SKPoint(r.MidX, r.Top);
        resizeHandles[2].Center = new SKPoint(r.Right, r.Top);
        resizeHandles[3].Center = new SKPoint(r.Right, r.MidY);
        resizeHandles[4].Center = new SKPoint(r.Right, r.Bottom);
        resizeHandles[5].Center = new SKPoint(r.MidX, r.Bottom);
        resizeHandles[6].Center = new SKPoint(r.Left, r.Bottom);
        resizeHandles[7].Center = new SKPoint(r.Left, r.MidY);
    }
}
