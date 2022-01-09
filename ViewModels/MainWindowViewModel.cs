using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using HocrEditor.Models;
using HocrEditor.Services;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        private ObservableCollection<HocrNodeViewModel>? previousSelectedNodes;

        public MainWindowViewModel()
        {
            DeleteCommand = new RelayCommand(DeleteSelectedNodes, CanEdit);
            MergeCommand = new RelayCommand(MergeSelectedNodes, CanEdit);
            CropCommand = new RelayCommand(CropSelectedNodes, CanEdit);

            PropertyChanged += HandlePropertyChanged;
        }

        public bool AutoCrop { get; set; } = true;

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

        public HocrDocumentViewModel? Document { get; set; }

        public IRelayCommand DeleteCommand { get; init; }
        public IRelayCommand MergeCommand { get; init; }

        public IRelayCommand CropCommand { get; init; }

        private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Document):
                    if (previousSelectedNodes != null)
                    {
                        previousSelectedNodes.CollectionChanged -= HandleSelectedNodesChanged;
                    }

                    if (Document != null)
                    {
                        Document.SelectedNodes.CollectionChanged += HandleSelectedNodesChanged;

                        previousSelectedNodes = Document.SelectedNodes;
                    }

                    break;
            }
        }

        private void HandleSelectedNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            DeleteCommand.NotifyCanExecuteChanged();
            MergeCommand.NotifyCanExecuteChanged();
            CropCommand.NotifyCanExecuteChanged();
        }

        private bool CanEdit() => Document?.SelectedNodes.Any() ?? false;

        private void DeleteSelectedNodes()
        {
            Debug.Assert(Document != null, $"{nameof(Document)} != null");

            var selectedNodes = Document.SelectedNodes.ToList();

            foreach (var node in selectedNodes)
            {
                Document.Remove(node);

                if (AutoCrop)
                {
                    CropParents(node);
                }
            }

            Document.ClearSelection();
        }

        private void MergeSelectedNodes()
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

            var nodes = new HierarchyTraverser<HocrNodeViewModel>(node => node.Children).ToEnumerable(first);

            first.BBox = NodeHelpers.CalculateUnionRect(nodes);

            Document.RemoveRange(rest);
        }

        private void CropSelectedNodes()
        {
            // Order nodes from the latest occurrence (deepest) to earliest, so if a chain of parent-children is selected,
            // the deepest child is cropped, then its parent and so on, bottom-up.
            var selectedNodes = Document?.SelectedNodes.OrderBy(node => -Document.Nodes.IndexOf(node)) ??
                                Enumerable.Empty<HocrNodeViewModel>();

            foreach (var node in selectedNodes)
            {
                CropNodeBounds(node);
            }
        }

        private void CropParents(HocrNodeViewModel node)
        {
            Debug.Assert(Document != null, $"{nameof(Document)} != null");
            Debug.Assert(node.ParentId != null, "node.ParentId != null");

            var parent = Document.NodeCache[node.ParentId];

            while (parent != null && parent.HocrNode.NodeType != HocrNodeType.Page)
            {
                CropNodeBounds(parent);

                parent = parent.ParentId == null ? null : Document.NodeCache[parent.ParentId];
            }
        }

        private static void CropNodeBounds(HocrNodeViewModel node)
        {
            node.BBox = NodeHelpers.CalculateUnionRect(node.Children);
        }
    }
}
