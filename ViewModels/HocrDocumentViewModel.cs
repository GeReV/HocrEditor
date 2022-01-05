using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HocrEditor.Models;
using HocrEditor.Services;

namespace HocrEditor.ViewModels
{
    public class HocrDocumentViewModel : ViewModelBase
    {
        public Dictionary<string, HocrNodeViewModel> NodeCache { get; }

        public RangeObservableCollection<HocrNodeViewModel> Nodes { get; }

        public RangeObservableCollection<HocrNodeViewModel> SelectedNodes { get; }

        public HocrDocumentViewModel(HocrDocument hocrDocument)
        {
            NodeCache = hocrDocument.Items.Select(node => new HocrNodeViewModel(node)).ToDictionary(node => node.Id);

            SelectedNodes = new RangeObservableCollection<HocrNodeViewModel>();

            Nodes = new RangeObservableCollection<HocrNodeViewModel>(NodeCache.Values);

            foreach (var (_, value) in NodeCache)
            {
                if (string.IsNullOrEmpty(value.ParentId))
                {
                    value.IsRoot = true;
                }
                else
                {
                    NodeCache[value.ParentId].Children.Add(value);
                }
            }
        }

        public void ClearSelection()
        {
            foreach (var selectedNode in SelectedNodes.ToList())
            {
                selectedNode.IsSelected = false;
            }

            SelectedNodes.Clear();
        }

        public void Remove(HocrNodeViewModel node)
        {
            var children = new HierarchyTraverser<HocrNodeViewModel>(item => item.Children).ToEnumerable(node).ToList();

            Nodes.RemoveRange(children);

            foreach (var child in children)
            {
                NodeCache.Remove(child.Id);
            }

            if (node.ParentId != null)
            {
                NodeCache[node.ParentId].Children.Remove(node);
            }
        }

        public void RemoveRange(IEnumerable<HocrNodeViewModel> nodes)
        {
            var nodeList = nodes.ToList();

            var traverser = new HierarchyTraverser<HocrNodeViewModel>(item => item.Children);

            var children = nodeList.SelectMany(node => traverser.ToEnumerable(node)).ToList();

            Nodes.RemoveRange(children);

            foreach (var child in children)
            {
                NodeCache.Remove(child.Id);
            }

            foreach (var node in nodeList)
            {
                if (node.ParentId != null)
                {
                    NodeCache[node.ParentId].Children.Remove(node);
                }
            }
        }
    }
}
