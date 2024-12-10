using SkiaSharp;

namespace HocrEditor.Controls;

internal class ResizeHandle
{
    private const int HANDLE_PADDING = 3;

    public ResizeHandle(SKPoint center, CardinalDirections direction)
    {
        Center = center;
        Direction = direction;
    }

    public SKRect GetRect()
    {
        var pos = Center;
        var rect = SKRect.Create(pos, new SKSize(HANDLE_PADDING * 2 + 1, HANDLE_PADDING * 2 + 1));

        rect.Offset(-HANDLE_PADDING - 1, -HANDLE_PADDING - 1);

        return rect;
    }

    public SKPoint Center { get; set; }

    public CardinalDirections Direction { get; set; }
}
