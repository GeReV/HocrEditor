using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;
using SkiaSharp;
using UserControl = System.Windows.Controls.UserControl;

namespace HocrEditor.Controls.Adjustments;

public partial class AdjustmentsControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty
        = DependencyProperty.Register(
            nameof(ViewModel),
            typeof(HocrPageViewModel),
            typeof(AdjustmentsControl),
            new FrameworkPropertyMetadata(
                defaultValue: null,
                ViewModelChanged
            )
        );

    public HocrPageViewModel? ViewModel
    {
        get => (HocrPageViewModel)GetValue(ViewModelProperty);
        set
        {
            if (value == null)
            {
                ClearValue(ViewModelProperty);
            }
            else
            {
                SetValue(ViewModelProperty, value);
            }
        }
    }

    private SKRectI clipRect = SKRectI.Empty;

    private SKShader shader = SKShader.CreateEmpty();

    public AdjustmentsControl()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateImage();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        shader.Dispose();
    }

    private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AdjustmentsControl)d;

        if (e.OldValue is HocrPageViewModel oldViewModel)
        {
            oldViewModel.PropertyChanged -= view.ViewModelOnPropertyChanged;
            oldViewModel.AdjustmentFilters.CollectionChanged -= view.AdjustmentFiltersCollectionChanged;
            oldViewModel.AdjustmentFilters.UnsubscribeItemPropertyChanged(view.AdjustmentFiltersChanged);
        }

        if (e.NewValue is not HocrPageViewModel viewModel)
        {
            return;
        }

        viewModel.PropertyChanged += view.ViewModelOnPropertyChanged;
        viewModel.AdjustmentFilters.CollectionChanged += view.AdjustmentFiltersCollectionChanged;
        viewModel.AdjustmentFilters.SubscribeItemPropertyChanged(view.AdjustmentFiltersChanged);

        if (!view.IsVisible)
        {
            return;
        }

        view.UpdateImage();
        view.Refresh();
    }


    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(HocrPageViewModel.Image), System.StringComparison.Ordinal))
        {
            UpdateImage();
        }
    }

    private void AdjustmentFiltersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateImage();
    }

    private void AdjustmentFiltersChanged(object? sender, PropertyChangedEventArgs propertyChangedEventArgs)
    {
        var shouldUpdate = propertyChangedEventArgs.PropertyName switch
        {
            nameof(ViewModelBase.IsChanged) => false,
            nameof(IImageFilter.IsEnabled) => true,
            { } propertyName when sender is IImageFilter imageFilter => imageFilter.ShouldUpdateImageOnPropertyChange(propertyName),
            _ => false,
        };


        if (shouldUpdate)
        {
            UpdateImage();
        }
    }

    private void Canvas_OnPaint(object? sender, ZoomPanPaintEventArgs e)
    {
        using var paint = new SKPaint();
        paint.Shader = shader;

        e.Surface.Canvas.Clear(SKColors.LightGray);
        e.Surface.Canvas.DrawRect(clipRect, paint);
    }

    private void UpdateImage()
    {
        Dispatcher.InvokeAsync(
            () =>
            {

                if (ViewModel == null)
                {
                    return;
                }

                _ = ViewModel.Image.GetBitmap()
                    .ContinueWith(
                        bitmapTask =>
                        {
                            var bitmap = bitmapTask.Result;

                            clipRect = bitmap.Info.Rect;

                            shader.Dispose();
                            shader = ViewModel.AdjustmentFilters.ApplyFilters(bitmap);

                            Refresh();
                        },
                        TaskScheduler.FromCurrentSynchronizationContext()
                    );
            },
            DispatcherPriority.Render
        );
    }

    private void Refresh()
    {
        Canvas.InvalidateVisual();
    }
}