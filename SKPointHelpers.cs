using System;
using SkiaSharp;

namespace HocrEditor;

public static class SKPointHelpers
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
}
