using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HocrEditor.Controls;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel(this);
        }

        private void Button_OnClick(object? sender, RoutedEventArgs e)
        {

        }

        private void Canvas_OnNodesChanged(object? sender, NodesChangedEventArgs e)
        {
            ViewModel.Document.CurrentPage?.UpdateNodesCommand.Execute(e.Changes);
        }

        private void Canvas_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                var items = e.AddedItems.Cast<HocrNodeViewModel>().ToList();
                ViewModel.Document.CurrentPage?.AppendSelectNodesCommand.TryExecute(items);

                foreach (var parent in items.SelectMany(n => n.Ascendants))
                {
                    // Close on deselect?
                    parent.IsExpanded = true;
                }
            }

            if (e.RemovedItems.Count > 0)
            {
                ViewModel.Document.CurrentPage?.DeselectNodesCommand.TryExecute(
                    e.RemovedItems.Cast<HocrNodeViewModel>().ToList()
                );
            }
        }

        private void OnNodeEdited(object? sender, NodesEditedEventArgs e)
        {
            ViewModel.Document.CurrentPage?.EditNodesCommand.Execute(e);
        }

        private void DocumentTreeView_OnNodesMoved(object? sender, NodesMovedEventArgs e)
        {
            ViewModel.Document.CurrentPage?.MoveNodesCommand.Execute(e);
        }

        private void CloseCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void SaveCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ViewModel.SaveCommand.TryExecute();
        }
    }
}
