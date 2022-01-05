using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using HocrEditor.Models;
using HocrEditor.Services;
using Microsoft.Toolkit.Mvvm.Input;
using Xamarin.Forms.Internals;

namespace HocrEditor.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        // Workaround for MultiSelectTreeView not working with Document.SelectedNodes directly.
        public ObservableCollection<HocrNodeViewModel>? SelectedNodes
        {
            get => Document?.SelectedNodes;
            set
            {
                if (Document != null && value != null)
                {
                    Document.SelectedNodes = new RangeObservableCollection<HocrNodeViewModel>(value);
                }
            }
        }

        public MainWindowViewModel()
        {
            DeleteCommand = new RelayCommand(Delete, CanEdit);
            MergeCommand = new RelayCommand(Merge, CanEdit);
        }

        public HocrDocumentViewModel? Document { get; set; }

        public ICommand DeleteCommand { get; init; }
        public ICommand MergeCommand { get; init; }

        private bool CanEdit() => Document?.SelectedNodes.Any() ?? false;

        private void Delete()
        {
            Debug.Assert(Document != null, $"{nameof(Document)} != null");

            var selectedNodes = Document.SelectedNodes.ToList();

            foreach (var node in selectedNodes)
            {
                Document.Remove(node);
            }

            Document.ClearSelection();
        }

        private void Merge()
        {
            Debug.Assert(Document != null, $"{nameof(Document)} != null");

            if (!Document.SelectedNodes.Any())
            {
                return;
            }

            var selectedNodes = Document.SelectedNodes.OrderBy(node => Document.Nodes.IndexOf(node)).ToList();

            // All child nodes will be merged into the first one.
            var first = selectedNodes.First();
            var rest = selectedNodes.Skip(1).ToArray();

            if (rest.Any(node => node.HocrNode.NodeType != first.HocrNode.NodeType))
            {
                // TODO: Show error.
                return;
            }

            var children = rest.SelectMany(node => node.Children).ToList();

            foreach (var parent in rest)
            {
                parent.Children.Clear();
            }

            foreach (var child in children)
            {
                child.ParentId = first.Id;

                first.Children.Add(child);
            }

            var bounds = first.BBox.ToSKRect();

            foreach (var child in new HierarchyTraverser<HocrNodeViewModel>(node => node.Children).ToEnumerable(first))
            {
                bounds.Union(child.BBox.ToSKRect());
            }

            first.BBox = new BoundingBox((int)bounds.Left, (int)bounds.Top, (int)bounds.Right, (int)bounds.Bottom);

            Document.RemoveRange(rest);
        }
    }
}
