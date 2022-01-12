using System.Windows;
using SkiaSharp;

namespace HocrEditor.Helpers;

public static class VectorHelpers
{
    public static void Deconstruct(this Vector v, out double x, out double y)
    {
        x = v.X;
        y = v.Y;
    }

    public static Point ToPoint(this Vector v) => new(v.X, v.Y);

    public static SKPoint ToSKPoint(this Vector v) => new((float)v.X, (float)v.Y);
}
