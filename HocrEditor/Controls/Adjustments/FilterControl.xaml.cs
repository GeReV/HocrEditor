using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using HocrEditor.Helpers;

namespace HocrEditor.Controls.Adjustments;

[ContentProperty(nameof(Content))]
public partial class FilterControl : UserControl
{
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked),
        typeof(bool),
        typeof(FilterControl),
        new FrameworkPropertyMetadata(defaultValue: true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
    );

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(FilterControl),
        new PropertyMetadata("Title")
    );

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public FilterControl()
    {
        InitializeComponent();
    }
}
