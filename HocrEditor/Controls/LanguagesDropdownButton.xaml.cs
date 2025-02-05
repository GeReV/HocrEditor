using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using HocrEditor.ViewModels;

namespace HocrEditor.Controls;

public partial class LanguagesDropdownButton : UserControl
{
    public static readonly DependencyProperty ItemsSourceProperty
        = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(ObservableCollection<TesseractLanguage>),
            typeof(LanguagesDropdownButton),
            new PropertyMetadata(propertyChangedCallback: null)
        );

    public ObservableCollection<TesseractLanguage>? ItemsSource
    {
        get => (ObservableCollection<TesseractLanguage>)GetValue(ItemsSourceProperty);
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

    public LanguagesDropdownButton()
    {
        InitializeComponent();
    }
}
