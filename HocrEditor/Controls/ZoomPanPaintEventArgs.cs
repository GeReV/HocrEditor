using System;
using SkiaSharp;

namespace HocrEditor.Controls;

public class ZoomPanPaintEventArgs(SKSurface surface, SKImageInfo info) : EventArgs
{
    public SKSurface Surface { get; } = surface;

    public SKImageInfo Info { get; } = info;
}
