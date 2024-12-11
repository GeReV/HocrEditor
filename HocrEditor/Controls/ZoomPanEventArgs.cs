using System;
using SkiaSharp;

namespace HocrEditor.Controls;

public class ZoomPanEventArgs(SKMatrix matrix) : EventArgs
{
    public SKMatrix Matrix { get; } = matrix;
}
