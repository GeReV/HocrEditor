using System.Collections.Generic;
using System.Linq;
using HocrEditor.Models;
using HocrEditor.Services;

namespace HocrEditor.ViewModels
{
    public class HocrDocumentViewModel : ViewModelBase
    {
        public Dictionary<string, HocrNodeViewModel> NodeCache { get; }

        public RangeObservableCollection<HocrNodeViewModel> Nodes { get; }

        public RangeObservableCollection<HocrNodeViewModel> SelectedNodes { get; set; }

        public HocrDocumentViewModel(HocrDocument hocrDocument)
        {
            NodeCache = BuildNodeCache(hocrDocument.Items.Prepend(hocrDocument.RootNode));

            Nodes = new RangeObservableCollection<HocrNodeViewModel>(NodeCache.Values);

            SelectedNodes = new RangeObservableCollection<HocrNodeViewModel>();
        }

        private static Dictionary<string, HocrNodeViewModel> BuildNodeCache(IEnumerable<IHocrNode> nodes)
        {
            var dictionary = new Dictionary<string, HocrNodeViewModel>();

            foreach (var node in nodes)
            {
                var hocrNodeViewModel = new HocrNodeViewModel(node);

                dictionary.Add(hocrNodeViewModel.Id, hocrNodeViewModel);

                if (string.IsNullOrEmpty(hocrNodeViewModel.ParentId))
                {
                    hocrNodeViewModel.IsRoot = true;
                }
                else
                {
                    var parent = dictionary[hocrNodeViewModel.ParentId];

                    hocrNodeViewModel.Parent = parent;

                    parent.Children.Add(hocrNodeViewModel);
                }
            }

            return dictionary;
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
            var children = node.Descendents.Prepend(node).ToList();

            Nodes.RemoveRange(children);

            foreach (var child in children)
            {
                NodeCache.Remove(child.Id);
            }

            node.Parent?.Children.Remove(node);
        }

        public void RemoveRange(IEnumerable<HocrNodeViewModel> nodes)
        {
            var nodeList = nodes.ToList();

            var children = nodeList.SelectMany(node => node.Descendents).ToList();

            Nodes.RemoveRange(children);

            foreach (var child in children)
            {
                NodeCache.Remove(child.Id);
            }

            foreach (var node in nodeList)
            {
                node.Parent?.Children.Remove(node);
            }
        }
    }
}
