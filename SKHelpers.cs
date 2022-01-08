using System;
using SkiaSharp;

namespace HocrEditor;

public static class SKHelpers
{
    public static void Clamp(this ref SKPoint p, SKRect bounds)
    {
        p.X = Math.Clamp(p.X, bounds.Left, bounds.Right);
        p.Y = Math.Clamp(p.Y, bounds.Top, bounds.Bottom);
    }

    public static void Clamp(this ref SKPoint p, SKSize size)
    {
        p.X = Math.Clamp(p.X, 0, size.Width);
        p.Y = Math.Clamp(p.Y, 0, size.Height);
    }

    public static void Clamp(this ref SKRect r, SKRect bounds)
    {
        var (left, right) = MinMax(bounds.Left, bounds.Right);
        var (top, bottom) = MinMax(bounds.Top, bounds.Bottom);

        r.Left = Math.Clamp(r.Left, left, right);
        r.Top = Math.Clamp(r.Top, top, bottom);
        r.Right = Math.Clamp(r.Right, left, right);
        r.Bottom = Math.Clamp(r.Bottom, top, bottom);
    }

    private static (float, float) MinMax(float a, float b) => a < b ? (a, b) : (b, a);
}
