using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HocrEditor.ImageProcessing;
using HocrEditor.Shaders;
using Optional;
using SkiaSharp;

namespace HocrEditor.ViewModels.Filters;

public sealed class HistogramScanFilter(bool automaticThreshold) : ImageFilterBase, IImageFilter
{
    private Option<CancellationTokenSource> updateCancellationTokenSource;

    private readonly ThresholdEffect thresholdEffect = new();

    private bool thresholdCalculated;

    public int MarkerPosition { get; set; } = 128;
    public int Threshold { get; set; } = 128;

    public IReadOnlyList<int> HistogramValues { get; private set; } = Array.Empty<int>();

    public static string Name => "Histogram Scan";

    public HistogramScanFilter() : this(automaticThreshold: true)
    {
    }

    protected override void PerformUpdate(SKShader source, SKImageInfo imageInfo)
    {
        if (automaticThreshold)
        {
            updateCancellationTokenSource.MatchSome(
                tokenSource =>
                {
                    tokenSource.Cancel();
                    tokenSource.Dispose();
                });

            var cts = new CancellationTokenSource();
            var ct = cts.Token;

            updateCancellationTokenSource = cts.Some();

            try
            {
                _ = Task.Run(
                        () =>
                        {
                            ct.ThrowIfCancellationRequested();

                            using var bitmap = new SKBitmap(imageInfo.Width, imageInfo.Height, isOpaque: true);
                            using var canvas = new SKCanvas(bitmap);

                            using var paint = new SKPaint();
                            paint.Shader = source;


                            canvas.DrawPaint(paint);

                            ct.ThrowIfCancellationRequested();

                            var bytes = bitmap.GetPixelSpan();
                            var bytesPerPixel = bitmap.BytesPerPixel;

                            return new Thresholder(bytes, bytesPerPixel);
                        },
                        ct
                    )
                    .ContinueWith(
                        thresholderTask =>
                        {
                            ct.ThrowIfCancellationRequested();

                            var thresholder = thresholderTask.Result;

                            HistogramValues = thresholder.Histogram.Values.ToArray();
                            MarkerPosition = (int)(thresholder.OtsuBinarization() * 255.0f);

                            if (automaticThreshold && !thresholdCalculated)
                            {
                                thresholdCalculated = true;

                                Threshold = MarkerPosition;
                            }
                        },
                        ct
                    );
            }
            catch (OperationCanceledException)
            {
                // Ignore.
            }
            finally
            {
                cts.Dispose();

                updateCancellationTokenSource = Option.None<CancellationTokenSource>();
            }
        }
    }

    public override SKShader Compose(SKShader source, SKImageInfo imageInfo)
    {
        thresholdEffect.Image = source;
        thresholdEffect.Threshold = Math.Clamp(Threshold / 255.0f, 0, 1);

        return thresholdEffect.ToShader();
    }

    public override bool ShouldUpdateImageOnPropertyChange(string propertyName) =>
        string.Equals(propertyName, "Threshold", StringComparison.Ordinal);

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
        {
            return;
        }

        thresholdEffect.Dispose();
    }
}
