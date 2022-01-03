using System;
using SkiaSharp;

namespace HocrEditor.Controls;

[Flags]
internal enum CardinalDirections
{
    North = 1 << 1,
    East = 1 << 2,
    South = 1 << 3,
    West = 1 << 4,
    NorthWest = North | West,
    NorthEast = North | East,
    SouthEast = South | East,
    SouthWest = South | West,
}

internal class ResizeHandle
{
    private const int HANDLE_PADDING = 3;

    public ResizeHandle(SKPoint center, CardinalDirections direction)
    {
        Center = center;
        Direction = direction;
    }

    public SKRect GetRect(SKMatrix? transformation = null)
    {
        var transform = transformation ?? SKMatrix.Identity;

        var pos = transform.MapPoint(Center);
        var rect = SKRect.Create(pos, new SKSize(HANDLE_PADDING * 2 + 1, HANDLE_PADDING * 2 + 1));

        rect.Offset(-HANDLE_PADDING - 1, -HANDLE_PADDING - 1);

        return rect;
    }

    public SKPoint Center { get; set; }

    public CardinalDirections Direction { get; set; }
}
