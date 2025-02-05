using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using HocrEditor.ViewModels;

namespace HocrEditor.Controls;

public partial class FiltersDropdownButton : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty
        = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IReadOnlyCollection<IAdjustmentFilterType>),
            typeof(FiltersDropdownButton),
            new PropertyMetadata(propertyChangedCallback: null)
        );

    public IReadOnlyCollection<IAdjustmentFilterType>? ItemsSource
    {
        get => (IReadOnlyCollection<IAdjustmentFilterType>)GetValue(ItemsSourceProperty);
        set
        {
            if (value == null)
            {
                ClearValue(ItemsSourceProperty);
            }
            else
            {
                SetValue(ItemsSourceProperty, value);
            }
        }
    }

    public FiltersDropdownButton()
    {
        InitializeComponent();
    }

    private void DeleteButton_OnClickHandler(object sender, RoutedEventArgs e)
    {
        FiltersDropdown.IsChecked = false;
    }
}

