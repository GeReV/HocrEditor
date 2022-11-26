using System.Threading.Tasks;
using System.Windows;
using HocrEditor.ImageProcessing;
using HocrEditor.Shaders;
using HocrEditor.ViewModels;
using SkiaSharp;
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

    private GRContext grContext;

    private SKImage? blurred;
    private SKSurface surface;

    public AdjustmentsControl()
    {
        grContext = GRContext.CreateGl();
        surface = SKSurface.Create(grContext, budgeted: true, new SKImageInfo(1, 1));

        InitializeComponent();

        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // TODO: Figure out proper disposal event.
        // blurred?.Dispose();
        //
        // surface.Dispose();
        // grContext.Dispose();
    }

    private static void ViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var view = (AdjustmentsControl)d;

        if (e.NewValue is not HocrPageViewModel viewModel)
        {
            return;
        }

        viewModel.Image.GetBitmap()
            .ContinueWith(
                async bitmapTask =>
                {
                    var bitmap = await bitmapTask;

                    view.surface.Dispose();
                    view.grContext.Dispose();

                    view.grContext = GRContext.CreateGl();
                    view.surface = SKSurface.Create(
                        view.grContext,
                        budgeted: true,
                        new SKImageInfo(bitmap.Width, bitmap.Height)
                    );

                    view.blurred?.Dispose();
                    view.blurred = CreateBlurredImage(view.surface, SKImage.FromBitmap(bitmap));

                    var thresholder = new Thresholder(view.blurred);
                    var threshold = thresholder.OtsuBinarization();

                    view.Histogram.MarkerPosition = view.Histogram.Value = (int)(threshold * 255);
                    view.Histogram.Values = thresholder.Histogram.Values.ToArray();

                    view.Canvas.InvalidateVisual();
                },
                TaskScheduler.FromCurrentSynchronizationContext()
            );
    }

    private void Canvas_OnPaint(object? sender, ZoomPanPaintEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        using var snapshot = surface.Snapshot();

        e.Surface.Canvas.DrawImage(snapshot, SKPoint.Empty);
    }

    private void Slider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateImage();
    }

    private void UpdateImage()
    {
        if (blurred is null)
        {
            return;
        }

        using var thresholdEffect = new ThresholdEffect(blurred.ToShader(), Histogram.Value / 255.0f);

        using var paintThreshold = new SKPaint { Shader = thresholdEffect.ToShader() };

        surface.Canvas.DrawPaint(paintThreshold);

        Refresh();
    }

    private void Refresh()
    {
        Canvas.InvalidateVisual();
        Canvas.UpdateLayout();
    }

    private static SKImage CreateBlurredImage(SKSurface surface, SKImage source)
    {
        using var gaussianBlur = new GaussianBlurEffect(source);
        using var grayscale = new GrayscaleEffect(gaussianBlur.ToShader());

        using var paintGrayscale = new SKPaint { Shader = grayscale.ToShader() };

        surface.Canvas.DrawPaint(paintGrayscale);

        return surface.Snapshot();
    }
}
