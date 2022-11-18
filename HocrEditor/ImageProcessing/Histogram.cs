using System;
using System.Linq;
using SkiaSharp;

namespace HocrEditor.ImageProcessing;

public class Histogram
{
    private const int LENGTH = 256;
    private readonly int[] values = new int[LENGTH];

    public Histogram(SKImage image)
    {
        using var pixmap = image.ToRasterImage(ensurePixelData: true).PeekPixels();

        var bytes = pixmap.GetPixelSpan();

        for (var i = 0; i < bytes.Length; i += pixmap.BytesPerPixel)
        {
            values[bytes[i]] += 1;
        }
    }

    public ReadOnlySpan<int> Values => values.AsSpan();

    public float[] Normalized()
    {
        var sum = values.Sum();

        return values.Select(v => v / (float)sum).ToArray();
    }
}
