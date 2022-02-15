using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using HocrEditor.Behaviors;
using HocrEditor.Controls;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Services;
using HocrEditor.ViewModels;

namespace HocrEditor
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public static readonly RoutedCommand MergeCommand = new();

        public MainWindow()
        {
            InitializeComponent();

            DataContext = new MainWindowViewModel(this);

            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await InitializeLanguages();
        }

        private async Task InitializeLanguages()
        {
            var tesseractPath = Settings.TesseractPath;

            if (tesseractPath == null)
            {
                return;
            }

            var languages = await new TesseractService(tesseractPath).GetLanguages();

            if (!languages.Any())
            {
                throw new InvalidOperationException("Tesseract returned no available languages.");
            }

            var selectedLanguages = Settings.TesseractSelectedLanguages;

            if (!selectedLanguages.Any())
            {
                const string english = "eng";

                selectedLanguages.Add(languages.Contains(english) ? english : languages.First());
            }

            languages.Sort(
                (a, b) =>
                {
                    var indexA = selectedLanguages.IndexOf(a);
                    var indexB = selectedLanguages.IndexOf(b);

                    return (indexA, indexB) switch
                    {
                        // Unselected languages are compared based on name.
                        (-1, -1) =>
                            // Scripts sink to bottom. Everything else is string compared.
                            (a.StartsWith("script/"), b.StartsWith("script/")) switch
                            {
                                (true, false) => 1,
                                (false, true) => -1,
                                _ => string.Compare(a, b, StringComparison.Ordinal),
                            },
                        // Selected languages rise up.
                        (-1, _) => 1,
                        (_, -1) => -1,
                        _ => indexA.CompareTo(indexB)
                    };
                }
            );

            foreach (var language in languages)
            {
                ViewModel.TesseractLanguages.Add(new TesseractLanguage(language, selectedLanguages.Contains(language)));
            }
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
            ViewModel.SaveCommand.TryExecute(e.Command == ApplicationCommands.SaveAs);
        }

        private void OpenCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            ViewModel.OpenCommand.TryExecute();
        }

        private void NodeVisibilityButton_OnClicked(object sender, RoutedEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.None)
            {
                return;
            }

            e.Handled = true;

            var button = (ToggleButton)sender;

            var currentNodeVisibility = (NodeVisibility)button.DataContext;

            var isChecked = button.IsChecked.GetValueOrDefault();

            var otherVisibilities = ViewModel.Document.NodeVisibility.Where(nv => nv != currentNodeVisibility).ToList();

            if (otherVisibilities.All(v => v.Visible == isChecked))
            {
                isChecked = !isChecked;
            }

            foreach (var nodeVisibility in otherVisibilities)
            {
                nodeVisibility.Visible = isChecked;
            }

            button.IsChecked = !isChecked;
        }

        private void MergeCommandBinding_OnExecuted(object sender, ExecutedRoutedEventArgs e)
        {
            // By default, we'll just return the focus to the original item.
            var focusOwner = Keyboard.FocusedElement;

            if (focusOwner is TreeViewItem treeViewItem)
            {
                // If we're focused on a TreeViewItem, keep the owner TreeView since the item may be detached from it.
                focusOwner = treeViewItem.FindVisualAncestor<TreeView>();
            }

            ViewModel.Document.CurrentPage?.MergeCommand.TryExecute(e.Parameter);

            if (focusOwner is TreeView treeView)
            {
                // Find the first selected item and focus on it.
                focusOwner = treeView
                    .FindVisualChildren<TreeViewItem>()
                    .FirstOrDefault(TreeViewMultipleSelectionBehavior.GetIsItemSelected);
            }

            Keyboard.Focus(focusOwner);
        }
    }
}
