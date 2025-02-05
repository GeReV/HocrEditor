using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using HocrEditor.Helpers;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using SkiaSharp.Views.WPF;
using UserControl = System.Windows.Controls.UserControl;

namespace HocrEditor.Controls;

public partial class HistogramControl : UserControl
{
    private const int HISTOGRAM_WIDTH = 256;

    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values),
        typeof(IReadOnlyList<int>),
        typeof(HistogramControl),
        new PropertyMetadata(Array.Empty<int>(), UpdateView, CoerceValues)
    );

    public IReadOnlyList<int> Values
    {
        get => (IReadOnlyList<int>)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(int),
        typeof(HistogramControl),
        new FrameworkPropertyMetadata(
            HISTOGRAM_WIDTH / 2,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            UpdateView,
            CoerceValue
        )
    );

    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty MarkerPositionProperty = DependencyProperty.Register(
        nameof(MarkerPosition),
        typeof(int),
        typeof(HistogramControl),
        new PropertyMetadata(0, UpdateView, CoerceValue)
    );

    public int MarkerPosition
    {
        get => (int)GetValue(MarkerPositionProperty);
        set => SetValue(MarkerPositionProperty, value);
    }

    public static readonly DependencyProperty BorderColorProperty = DependencyProperty.Register(
        nameof(BorderColor),
        typeof(Color),
        typeof(HistogramControl),
        new PropertyMetadata(Colors.DarkGray, UpdateView)
    );

    public Color BorderColor
    {
        get => (Color)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public static readonly DependencyProperty LineColorProperty = DependencyProperty.Register(
        nameof(LineColor),
        typeof(Color),
        typeof(HistogramControl),
        new PropertyMetadata(Colors.Black, UpdateView)
    );

    public Color LineColor
    {
        get => (Color)GetValue(LineColorProperty);
        set => SetValue(LineColorProperty, value);
    }

    public static readonly DependencyProperty TickColorProperty = DependencyProperty.Register(
        nameof(TickColor),
        typeof(Color),
        typeof(HistogramControl),
        new PropertyMetadata(Colors.LightGray, UpdateView)
    );

    public Color TickColor
    {
        get => (Color)GetValue(TickColorProperty);
        set => SetValue(TickColorProperty, value);
    }

    public static readonly DependencyProperty MarkerColorProperty = DependencyProperty.Register(
        nameof(MarkerColor),
        typeof(Color),
        typeof(HistogramControl),
        new PropertyMetadata(Colors.LightGreen, UpdateView)
    );

    public Color MarkerColor
    {
        get => (Color)GetValue(MarkerColorProperty);
        set => SetValue(MarkerColorProperty, value);
    }

    public static readonly DependencyProperty HistogramMarginProperty = DependencyProperty.Register(
        nameof(HistogramMargin),
        typeof(Thickness),
        typeof(HistogramControl),
        new PropertyMetadata(new Thickness(8.0, 0.0, 8.0, 4.0), UpdateView)
    );

    public Thickness HistogramMargin
    {
        get => (Thickness)GetValue(HistogramMarginProperty);
        set => SetValue(HistogramMarginProperty, value);
    }

    public HistogramControl()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        foreach (var element in this.FindVisualChildren<TickBar>())
        {
            element.Visibility = Visibility.Collapsed;
        }
    }

    private static void UpdateView(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (HistogramControl)d;

        control.Surface.InvalidateVisual();
    }

    private static object CoerceValues(DependencyObject d, object baseValue)
    {
        if (baseValue is not int[] array)
        {
            return Array.Empty<int>();
        }

        if (array.Length > 256)
        {
            return array[..256];
        }

        return array;
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        if (baseValue is not int value)
        {
            return default(int);
        }

        return Math.Clamp(value, 0, HISTOGRAM_WIDTH);
    }

    private void Canvas_OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var info = e.Info;
        var canvas = e.Surface.Canvas;

        using var font = new SKFont(SKTypeface.Default, 10.0f);

        using var paint = new SKPaint();
        paint.StrokeWidth = 0;

        var x = Math.Max((int)HistogramMargin.Left, (info.Width - HISTOGRAM_WIDTH) / 2);
        var histogramRect = new SKRectI(
            x,
            (int)HistogramMargin.Top,
            x + HISTOGRAM_WIDTH,
            (int)(info.Height - HistogramMargin.Bottom - font.Size)
        );

        canvas.Clear(SKColors.White);

        paint.Color = TickColor.ToSKColor();
        DrawTicks(canvas, histogramRect, paint);

        paint.Color = BorderColor.ToSKColor();
        DrawBorder(canvas, histogramRect, paint);

        paint.Color = LineColor.ToSKColor();
        DrawHistogramLines(canvas, histogramRect, Values, paint);

        paint.Color = MarkerColor.ToSKColor();
        DrawMarker(canvas, histogramRect, MarkerPosition, paint);

        paint.Color = LineColor.ToSKColor();
        var labelTop = (float)(histogramRect.Bottom + HistogramMargin.Bottom);

        var text = "0";
        font.MeasureText(text, out var textBounds, paint);
        DrawLabel(text, histogramRect.Left - 1, labelTop + textBounds.Height, canvas, paint, font);

        text = "255";
        font.MeasureText(text, out textBounds, paint);
        DrawLabel(text, histogramRect.Right + 1 - textBounds.Width, labelTop + textBounds.Height, canvas, paint, font);
    }

    private static void DrawMarker(SKCanvas canvas, SKRectI histogramRect, int markerPosition, SKPaint paint)
    {
        var x = histogramRect.Left + markerPosition;

        canvas.DrawLine(x, histogramRect.Bottom, x, histogramRect.Top, paint);
    }

    private static void DrawTicks(SKCanvas canvas, SKRectI histogramRect, SKPaint paint)
    {
        const int tickCount = 4;
        for (var i = 1; i < tickCount; i++)
        {
            var x = histogramRect.Left + (i * HISTOGRAM_WIDTH / tickCount);

            canvas.DrawLine(x, histogramRect.Bottom, x, histogramRect.Top, paint);
        }
    }

    private static void DrawHistogramLines(
        SKCanvas canvas,
        SKRectI histogramRect,
        IReadOnlyList<int> values,
        SKPaint paint
    )
    {
        var max = values.Count > 0 ? values.Max() : 1.0f;
        for (var i = 0; i < values.Count; i++)
        {
            var position = histogramRect.Left + i;
            var top = histogramRect.Bottom - values[i] / max * histogramRect.Height;

            canvas.DrawLine(position, top, position, histogramRect.Bottom, paint);
        }
    }

    private static void DrawBorder(SKCanvas canvas, SKRectI histogramRect, SKPaint paint)
    {
        canvas.DrawLine(
            histogramRect.Left - 1,
            histogramRect.Bottom,
            histogramRect.Right + 1,
            histogramRect.Bottom,
            paint
        );
        canvas.DrawLine(
            histogramRect.Left - 1,
            histogramRect.Top,
            histogramRect.Left - 1,
            histogramRect.Bottom,
            paint
        );
        canvas.DrawLine(
            histogramRect.Right + 1,
            histogramRect.Top,
            histogramRect.Right + 1,
            histogramRect.Bottom + 1,
            paint
        );
    }

    private static void DrawLabel(string text, float x, float y, SKCanvas canvas, SKPaint paint, SKFont font)
    {
        canvas.DrawText(
            text,
            x,
            y,
            SKTextAlign.Left,
            font,
            paint
        );
    }

    #region Events

    /// <summary>
    /// Event correspond to Value changed event
    /// </summary>
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(ValueChanged),
        RoutingStrategy.Bubble,
        typeof(RoutedPropertyChangedEventHandler<double>),
        typeof(HistogramControl)
    );

    /// <summary>
    /// Add / Remove ValueChangedEvent handler
    /// </summary>
    [Category("Behavior")]
    public event RoutedPropertyChangedEventHandler<double> ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    #endregion Events

    private void Slider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        RaiseEvent(new RoutedPropertyChangedEventArgs<double>(e.OldValue, e.NewValue, ValueChangedEvent));
    }
}
