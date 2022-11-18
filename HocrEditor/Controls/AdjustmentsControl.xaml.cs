using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using HocrEditor.ImageProcessing;
using HocrEditor.Shaders;
using HocrEditor.ViewModels;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using UserControl = System.Windows.Controls.UserControl;

namespace HocrEditor.Controls;

public partial class AdjustmentsControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty
        = DependencyProperty.Register(
            nameof(ViewModel),
            typeof(HocrPageViewModel),
            typeof(AdjustmentsControl),
            new PropertyMetadata(
                null,
                ViewModelChanged
            )
        );

    private GRContext grContext;
    private SKSize screenCanvasSize;

    private SKImage? blurred;
    private SKSurface surface;

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

    public AdjustmentsControl()
    {
        grContext = GRContext.CreateGl();
        surface = SKSurface.Create(grContext, budgeted: true, new SKImageInfo(1, 1));

        InitializeComponent();

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        blurred?.Dispose();

        surface.Dispose();
        grContext.Dispose();
    }

    private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AdjustmentsControl)d;

        if (e.NewValue is not HocrPageViewModel viewModel)
        {
            return;
        }

        view.Dispatcher.BeginInvoke(
            async () =>
            {
                var bitmap = await viewModel.Image.GetBitmap();

                view.blurred?.Dispose();
                view.blurred = view.CreateBlurredImage(SKImage.FromBitmap(bitmap));

                view.Surface.InvalidateVisual();
            },
            DispatcherPriority.Normal
        );
    }

    private void Canvas_OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        if (ViewModel is null || blurred is null)
        {
            return;
        }

        var size = e.Info.Size;

        if (screenCanvasSize != size)
        {
            surface.Dispose();
            grContext.Dispose();

            grContext = GRContext.CreateGl();
            surface = SKSurface.Create(grContext, budgeted: true, new SKImageInfo(size.Width, size.Height));

            var bitmap = ViewModel.Image.GetBitmap().GetAwaiter().GetResult();
            var image = SKImage.FromPixelCopy(bitmap.Info, bitmap.GetPixelSpan());

            blurred?.Dispose();
            blurred = CreateBlurredImage(image);

            var thresholder = new Thresholder(blurred);
            var threshold = thresholder.OtsuBinarization();

            Histogram.MarkerPosition = Histogram.Value = (int)(threshold * 255);
            Histogram.Values = thresholder.Histogram.Values.ToArray();

            screenCanvasSize = e.Info.Size;
        }

        using var thresholdEffect = new ThresholdEffect(blurred.ToShader(), Histogram.Value / 255.0f);

        using var paintThreshold = new SKPaint { Shader = thresholdEffect.ToShader() };

        surface.Canvas.DrawPaint(paintThreshold);

        using var snapshot = surface.Snapshot();

        e.Surface.Canvas.DrawImage(snapshot, SKPoint.Empty);
    }

    private void Slider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        Surface.InvalidateVisual();
    }

    private SKImage CreateBlurredImage(SKImage source)
    {
        using var gaussianBlur = new GaussianBlurEffect(source);
        using var grayscale = new GrayscaleEffect(gaussianBlur.ToShader());

        using var paintGrayscale = new SKPaint { Shader = grayscale.ToShader() };

        surface.Canvas.DrawPaint(paintGrayscale);

        return surface.Snapshot();
    }
}
