using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace HocrEditor.Controls.Adjustments;

[ContentProperty(nameof(Children))]
public partial class FilterControl : UserControl
{
    public static readonly DependencyPropertyKey ChildrenProperty = DependencyProperty.RegisterReadOnly(
        nameof(Children),
        typeof(UIElementCollection),
        typeof(FilterControl),
        new PropertyMetadata());

    public UIElementCollection Children
    {
        get => (UIElementCollection)GetValue(ChildrenProperty.DependencyProperty);
        private init => SetValue(ChildrenProperty, value);
    }

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

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(FilterControl),
        new PropertyMetadata(default(string))
    );

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public FilterControl()
    {
        InitializeComponent();

        Children = PART_Host.Children;
    }
}
