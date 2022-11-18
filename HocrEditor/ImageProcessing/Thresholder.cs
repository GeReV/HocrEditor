using System.Linq;
using HocrEditor.Helpers;
using SkiaSharp;

namespace HocrEditor.ImageProcessing;

public class Thresholder
{
    public readonly Histogram Histogram;

    public Thresholder(SKImage snapshot)
    {
        Histogram = new Histogram(snapshot);
    }
    public float OtsuBinarization()
    {
        var bins = Enumerable.Range(0, 256).ToArray();

        var min = float.PositiveInfinity;
        var thresh = -1;

        var norm = Histogram.Normalized();
        var q = norm.CumulativeSum().ToArray();

        for (var i = 1; i < 256; i++)
        {
            var r1 = 1..i;
            var r2 = (i + 1)..256;

            var p1 = norm[r1];
            var p2 = norm[r2];

            var b1 = bins[r1];
            var b2 = bins[r2];

            var q1 = q[i];
            var q2 = q[255] - q[i];

            if (q1 < 1e-6 || q2 < 1e-6)
            {
                continue;
            }

            // finding means and variances

            var m1 = p1.Zip(b1).Sum(pair => pair.First * pair.Second) / q1;
            var m2 = p2.Zip(b2).Sum(pair => pair.First * pair.Second) / q2;

            var v1 = p1.Zip(b1)
                .Sum(
                    pair =>
                    {
                        var (p, b) = pair;
                        var k = b - m1;
                        return k * k * p;
                    }
                ) / q1;
            var v2 = p2
                .Zip(b2)
                .Sum(
                    pair =>
                    {
                        var (p, b) = pair;
                        var k = b - m2;
                        return k * k * p;
                    }
                ) / q2;

            // calculates the minimization function
            var fn = v1 * q1 + v2 * q2;

            if (fn < min)
            {
                min = fn;
                thresh = i;
            }
        }

        return thresh / 256.0f;
    }
}
