using System;
using System.Net.Cache;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HocrEditor.Controls;

public class LazyLoadImage : Image
{
    public static readonly DependencyProperty UriSourceProperty = DependencyProperty.Register(
        nameof(UriSource),
        typeof(Uri),
        typeof(LazyLoadImage),
        new FrameworkPropertyMetadata(
            null,
            UriSourcePropertyChanged
        )
    );

    public Uri? UriSource
    {
        get => (Uri?)GetValue(UriSourceProperty);
        set => SetValue(UriSourceProperty, value);
    }

    private static void UriSourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (Equals(e.NewValue, e.OldValue))
        {
            return;
        }

        var image = (LazyLoadImage)d;

        if (e.NewValue == null)
        {
            image.Source = null;

            return;
        }

        Task.Run(
                () =>
                {
                    var bitmapImage = new BitmapImage((Uri)e.NewValue, new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable))
                    {
                        DecodePixelWidth = (int)image.ActualWidth,
                        CacheOption = BitmapCacheOption.OnLoad
                    };

                    bitmapImage.Freeze();

                    return bitmapImage;
                }
            )
            .ContinueWith(
                bitmapTask => image.Dispatcher.BeginInvoke(
                    async () => image.Source = await bitmapTask,
                    DispatcherPriority.Background
                )
            );
    }

    protected override Size MeasureOverride(Size constraint)
    {
        var size = base.MeasureOverride(constraint);

        if (size.Width > 0 && size.Height > 0)
        {
            return size;
        }

        size.Width = size.Height = constraint.Width;

        return size;
    }
}
