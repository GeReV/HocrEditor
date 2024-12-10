using System;
using System.Linq;
using SkiaSharp;

namespace HocrEditor.ImageProcessing;

public class Histogram
{
    private const int LENGTH = 256;
    private readonly int[] values = new int[LENGTH];

    public Histogram(ReadOnlySpan<byte> bytes, int bytesPerPixel)
    {
        for (var i = 0; i < bytes.Length; i += bytesPerPixel)
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
