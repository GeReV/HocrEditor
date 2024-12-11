using SkiaSharp;

namespace HocrEditor.Helpers;

public static class SKCanvasExtensions
{
    private const float SELECTION_DASH_LENGTH = 5f;

    private static readonly SKPaint DashPaint = new()
    {
        Style = SKPaintStyle.Stroke,
        Color = SKColors.Black,
        StrokeWidth = 0,
        PathEffect = SKPathEffect.CreateDash(new[] { SELECTION_DASH_LENGTH, SELECTION_DASH_LENGTH }, 0f),
    };

    private static SKPaint StrokePaintWithColor(SKColor color) =>
        new()
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 0,
            Color = color == SKColor.Empty ? SKColors.White : color,
        };

    public static void DrawDashedPath(this SKCanvas canvas, SKPath path, SKColor color = default)
    {
        using var paint = StrokePaintWithColor(color);

        canvas.DrawPath(path, paint);
        canvas.DrawPath(path, DashPaint);
    }


    public static void DrawDashedLine(this SKCanvas canvas, SKPoint p0, SKPoint p1, SKColor color = default) => DrawDashedLine(canvas, p0.X, p0.Y, p1.X, p1.Y, color);

    public static void DrawDashedLine(this SKCanvas canvas, float x0, float y0, float x1, float y1, SKColor color = default)
    {
        using var paint = StrokePaintWithColor(color);

        canvas.DrawLine(x0, y0, x1, y1, paint);
        canvas.DrawLine(x0, y0, x1, y1, DashPaint);
    }
}
