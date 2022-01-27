using System.Collections;
using System.Windows;
using System.Windows.Input;
using HocrEditor.ViewModels;

namespace HocrEditor.Controls;

public partial class DocumentPageList
{
    private HocrDocumentViewModel ViewModel => (HocrDocumentViewModel)DataContext;

    public static readonly DependencyProperty ItemsSourceProperty
        = DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(DocumentPageList),
            new FrameworkPropertyMetadata(null)
        );

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable)GetValue(ItemsSourceProperty);
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

    public DocumentPageList()
    {
        InitializeComponent();
    }

    private void DeleteCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;

        if (MessageBox.Show(
                "Are you sure you would like to delete this page?",
                "Delete Page",
                MessageBoxButton.YesNo
            ) != MessageBoxResult.Yes)
        {
            return;
        }

        ViewModel.DeletePage((HocrPageViewModel?)PageList.SelectedItem);
    }

    private void DeleteCommandBinding_OnCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.Handled = true;
        e.CanExecute = ViewModel.CanDeletePage((HocrPageViewModel?)PageList.SelectedItem);
    }
}
