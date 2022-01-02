using System.Windows;

namespace HocrEditor;

public static class PointHelpers
{
    public static void Deconstruct(this Point p, out double x, out double y)
    {
        x = p.X;
        y = p.Y;
    }

    public static Vector ToVector(this Point p) => new(p.X, p.Y);
}
