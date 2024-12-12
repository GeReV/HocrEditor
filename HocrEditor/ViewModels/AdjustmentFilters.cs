using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using HocrEditor.Core;
using HocrEditor.Helpers;
using Optional;
using Optional.Unsafe;
using SkiaSharp;

namespace HocrEditor.ViewModels;

public sealed class AdjustmentFilters : ObservableCollection<ImageFilterBase>, IDisposable
{
    private Option<SKBitmap> thresholdedBitmap;

    public AdjustmentFilters() : base([
        // new GrayscaleFilter(),
        // new GaussianBlurFilter { KernelSize = 3 },
        new HistogramScanFilter(),
    ])
    {
        CollectionChanged += OnCollectionChanged;
        this.SubscribeItemPropertyChanged(OnPropertyChanged);
    }

    // TODO: Extract?
    public SKBitmap GenerateThresholdedImage(SKBitmap source)
    {
        return thresholdedBitmap.ValueOr(
            () =>
            {
                using var paint = new SKPaint();
                paint.Shader = ApplyFilters(source);

                var bitmap = new SKBitmap(source.Width, source.Height, isOpaque: true);
                using var canvas = new SKCanvas(bitmap);

                canvas.DrawPaint(paint);

                thresholdedBitmap.MatchSome(prev => prev.Dispose());
                thresholdedBitmap = bitmap.Some();

                return bitmap;
            }
        );
    }

    public SKShader ApplyFilters(SKBitmap bitmap)
    {
        var shader = SKShader.CreateBitmap(bitmap);

        foreach (var filter in this.Where(filter => filter.IsEnabled))
        {
            filter.Update(shader, bitmap.Info);

            shader = filter.Compose(shader, bitmap.Info);
        }

        return shader;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        thresholdedBitmap = Option.None<SKBitmap>();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Ensure.IsNotNull(sender);

        thresholdedBitmap = Option.None<SKBitmap>();

        UpdateDownstreamFilters((ImageFilterBase)sender);

        OnPropertyChanged(EventArgsCache.AnyPropertyChanged);
    }

    private void UpdateDownstreamFilters(ImageFilterBase sender)
    {
        var index = IndexOf(sender);

        for (var i = index + 1; i < Count; i++)
        {
            this[i].MarkForUpdate();
        }
    }

    public void Dispose()
    {
        CollectionChanged -= OnCollectionChanged;
        this.UnsubscribeItemPropertyChanged(OnPropertyChanged);
    }
}
