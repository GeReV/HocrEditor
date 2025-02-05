using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HocrEditor.Controls;

public class TitledPanel : HeaderedContentControl
{
    public static readonly DependencyProperty HeaderBackgroundProperty = DependencyProperty.Register(
        nameof(HeaderBackground),
        typeof(Brush),
        typeof(TitledPanel),
        new FrameworkPropertyMetadata(defaultValue: null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public Brush HeaderBackground
    {
        get => (Brush)GetValue(HeaderBackgroundProperty);
        set => SetValue(HeaderBackgroundProperty, value);
    }

    public static readonly DependencyProperty HeaderBorderBrushProperty = DependencyProperty.Register(
        nameof(HeaderBorderBrush),
        typeof(Brush),
        typeof(TitledPanel),
        new FrameworkPropertyMetadata(defaultValue: null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public Brush HeaderBorderBrush
    {
        get => (Brush)GetValue(HeaderBorderBrushProperty);
        set => SetValue(HeaderBorderBrushProperty, value);
    }

    public static readonly DependencyProperty HeaderBorderThicknessProperty = DependencyProperty.Register(
        nameof(HeaderBorderThickness),
        typeof(Thickness),
        typeof(TitledPanel),
        new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public Thickness HeaderBorderThickness
    {
        get => (Thickness)GetValue(HeaderBorderThicknessProperty);
        set => SetValue(HeaderBorderThicknessProperty, value);
    }

    public static readonly DependencyProperty HeaderCursorProperty = DependencyProperty.Register(
        nameof(HeaderCursor),
        typeof(Cursor),
        typeof(TitledPanel),
        new PropertyMetadata(default(Cursor))
    );

    public Cursor HeaderCursor
    {
        get => (Cursor)GetValue(HeaderCursorProperty);
        set => SetValue(HeaderCursorProperty, value);
    }

    public static readonly DependencyProperty HeaderPaddingProperty = DependencyProperty.Register(
        nameof(HeaderPadding),
        typeof(Thickness),
        typeof(TitledPanel),
        new FrameworkPropertyMetadata(default(Thickness), FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender)
    );

    public Thickness HeaderPadding
    {
        get => (Thickness)GetValue(HeaderPaddingProperty);
        set => SetValue(HeaderPaddingProperty, value);
    }
}
