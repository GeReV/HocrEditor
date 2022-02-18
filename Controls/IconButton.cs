using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HocrEditor.Controls;

public class IconButton : Button
{
    private static ResourceKey? _cacheButtonStyle;

    public static ResourceKey ToolBarIconButtonStyleKey => _cacheButtonStyle ??= new ComponentResourceKey(
        typeof(IconButton),
        nameof(ToolBarIconButtonStyleKey)
    );

    /// <summary>
    /// DependencyProperty for Image Source property.
    /// </summary>
    /// <seealso cref="Image.Source" />
    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(
            nameof(ImageSource),
            typeof(ImageSource),
            typeof(IconButton),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets/Sets the Source on this Image.
    ///
    /// The Source property is the ImageSource that holds the actual image drawn.
    /// </summary>
    public ImageSource ImageSource
    {
        get => (ImageSource)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    static IconButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(IconButton),
            new FrameworkPropertyMetadata(typeof(IconButton))
        );
    }
}
