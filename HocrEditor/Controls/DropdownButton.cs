using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace HocrEditor.Controls;

[TemplatePart(Name = "PART_Popup", Type = typeof(Popup))]
public class DropdownButton : ToggleButton
{
    static DropdownButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DropdownButton), new FrameworkPropertyMetadata(typeof(DropdownButton)));
    }

    public static readonly DependencyProperty PopupProperty =
        DependencyProperty.Register(
            nameof(Popup),
            typeof(object),
            typeof(DropdownButton),
            new FrameworkPropertyMetadata(defaultValue: null)
        );

    /// <summary>
    ///     Header is the data used to for the header of each item in the control.
    /// </summary>
    [Bindable(true), Category("Content")]
    [Localizability(LocalizationCategory.Label)]
    public object Popup
    {
        get => GetValue(PopupProperty);
        set => SetValue(PopupProperty, value);
    }

    public static readonly DependencyProperty PopupTemplateProperty =
        DependencyProperty.Register(
            nameof(PopupTemplate),
            typeof(DataTemplate),
            typeof(DropdownButton),
            new FrameworkPropertyMetadata(defaultValue: null)
        );

    /// <summary>
    ///     PopupTemplate is the template used to display the <seealso cref="Popup"/>.
    /// </summary>
    [Bindable(true), Category("Content")]
    public DataTemplate PopupTemplate
    {
        get => (DataTemplate)GetValue(PopupTemplateProperty);
        set => SetValue(PopupTemplateProperty, value);
    }

    public static readonly DependencyProperty PopupTemplateSelectorProperty =
        DependencyProperty.Register(
            nameof(PopupTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(DropdownButton),
            new FrameworkPropertyMetadata(defaultValue: null)
        );

    /// <summary>
    ///     PopupTemplateSelector allows the application writer to provide custom logic
    ///     for choosing the template used to display the <seealso cref="Popup"/>.
    /// </summary>
    /// <remarks>
    ///     This property is ignored if <seealso cref="PopupTemplate"/> is set.
    /// </remarks>
    [Bindable(true), Category("Content")]
    public DataTemplateSelector PopupTemplateSelector
    {
        get => (DataTemplateSelector)GetValue(PopupTemplateSelectorProperty);
        set => SetValue(PopupTemplateSelectorProperty, value);
    }
}
