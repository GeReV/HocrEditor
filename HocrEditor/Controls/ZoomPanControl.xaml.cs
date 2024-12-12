using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;

namespace HocrEditor.Controls;

public partial class ZoomPanControl : UserControl
{
    public event EventHandler<ZoomPanEventArgs>? ZoomPan;

    public event EventHandler<ZoomPanPaintEventArgs>? Paint;

    public static readonly DependencyProperty TransformProperty = DependencyProperty.Register(
        nameof(Transform),
        typeof(SKMatrix),
        typeof(ZoomPanControl),
        new FrameworkPropertyMetadata(SKMatrix.Identity, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, MatrixChanged)
    );

    private static void MatrixChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (ZoomPanControl)d;

        if (e.NewValue is not SKMatrix matrix)
        {
            return;
        }

        control.InverseTransformation = matrix.Invert();

        control.ScaleTransformation = control.ScaleTransformation with
        {
            ScaleX = matrix.ScaleX,
            ScaleY = matrix.ScaleY,
        };

        control.InverseScaleTransformation = control.InverseScaleTransformation with
        {
            ScaleX = control.InverseTransformation.ScaleX,
            ScaleY = control.InverseTransformation.ScaleY
        };

        control.Refresh();
    }

    public SKMatrix Transform
    {
        get => (SKMatrix)GetValue(TransformProperty);
        set => SetValue(TransformProperty, value);
    }

    private static readonly SKSize CenterPadding = new(-10.0f, -10.0f);

    public SKMatrix InverseTransformation { get; private set; } = SKMatrix.Identity;

    private SKMatrix ScaleTransformation { get; set; } = SKMatrix.Identity;
    private SKMatrix InverseScaleTransformation { get; set; } = SKMatrix.Identity;

    private bool isPanning;

    private SKPoint dragStart = SKPoint.Empty;
    private SKPoint offsetStart = SKPoint.Empty;

    public ZoomPanControl()
    {
        InitializeComponent();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        Refresh();
    }

    private void Refresh()
    {
        _ = Dispatcher.InvokeAsync(Surface.InvalidateVisual, DispatcherPriority.Render);
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);

        Mouse.Capture(this);

        var position = e.GetPosition(this).ToSKPoint();

        if (e.ChangedButton != MouseButton.Middle)
        {
            // Noop.
            return;
        }

        e.Handled = true;

        dragStart = position;

        isPanning = true;

        offsetStart = Transform.MapPoint(SKPoint.Empty);

        Refresh();
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);

        ReleaseMouseCapture();

        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        if (!isPanning)
        {
            return;
        }

        e.Handled = true;

        isPanning = false;

        Refresh();
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        var position = e.GetPosition(this).ToSKPoint();

        var delta = InverseScaleTransformation.MapPoint(position - dragStart);

        if (isPanning)
        {
            e.Handled = true;

            var newLocation = InverseTransformation.MapPoint(offsetStart) + delta;

            UpdateTransformation(SKMatrix.CreateTranslation(newLocation.X, newLocation.Y));

            OnZoomPan();
        }

        Refresh();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        var delta = Math.Sign(e.Delta) * 3;

        var pointerP = e.GetPosition(this).ToSKPoint();
        var p = InverseTransformation.MapPoint(pointerP);

        var newScale = (float)Math.Pow(2, delta * 0.05f);

        UpdateTransformation(SKMatrix.CreateScale(newScale, newScale, p.X, p.Y));

        OnZoomPan();

        Refresh();
    }

    private void Canvas_OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        var color = SKColors.LightGray;
        if (Background is SolidColorBrush brush)
        {
            color = brush.Color.ToSKColor();
        }

        e.Surface.Canvas.Clear(color);
        e.Surface.Canvas.SetMatrix(Transform);

        Paint?.Invoke(this, new ZoomPanPaintEventArgs(e.Surface, e.Info));
    }

    private void UpdateTransformation(SKMatrix matrix)
    {
        const float transformationScaleMin = 1 / 32.0f;
        const float transformationScaleMax = 8.0f;

        var nextTransformation = Transform.PreConcat(matrix);

        // TODO: Clamping to the exact zoom limits is not as straightforward as setting the scale, as the translation
        //  needs to adapt. Figure it out.
        if (nextTransformation.ScaleX < Transform.ScaleX &&
            nextTransformation.ScaleX < transformationScaleMin)
        {
            return;
        }

        if (nextTransformation.ScaleX > Transform.ScaleX &&
            nextTransformation.ScaleX > transformationScaleMax)
        {
            return;
        }

        Transform = nextTransformation;
    }

    private void OnZoomPan()
    {
        ZoomPan?.Invoke(this, new ZoomPanEventArgs(Transform));
    }

    // TODO: Make this happen only after first render somehow.
    public void CenterTransformation(SKRect rect)
    {
        var controlSize = SKRect.Create(RenderSize.ToSKSize());

        controlSize.Inflate(CenterPadding);

        var fitBounds = controlSize.AspectFit(rect.Size);

        var resizeFactor = Math.Min(
            fitBounds.Width / rect.Width,
            fitBounds.Height / rect.Height
        );

        // resizeFactor = (float)Math.Log(1.0f + resizeFactor) * 0.33f;

        var scaleMatrix = SKMatrix.CreateScale(
            resizeFactor,
            resizeFactor
        );

        rect = scaleMatrix.MapRect(rect);

        var transformation =
            SKMatrix.CreateTranslation(controlSize.MidX - rect.MidX, controlSize.MidY - rect.MidY)
                .PreConcat(scaleMatrix);

        Transform = transformation;

        Refresh();
    }
}
