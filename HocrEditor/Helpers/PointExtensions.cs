using System;
using System.Windows;
using Rect = HocrEditor.Models.Rect;

namespace HocrEditor.Helpers;

public static class PointExtensions
{
    public static void Clamp(this ref Point p, Rect bounds)
    {
        p.X = Math.Clamp(p.X, bounds.Left, bounds.Right);
        p.Y = Math.Clamp(p.Y, bounds.Top, bounds.Bottom);
    }

    public static void Clamp(this ref Point p, Size size)
    {
        p.X = Math.Clamp(p.X, 0, size.Width);
        p.Y = Math.Clamp(p.Y, 0, size.Height);
    }

    private static (float, float) MinMax(float a, float b) => a < b ? (a, b) : (b, a);
}
