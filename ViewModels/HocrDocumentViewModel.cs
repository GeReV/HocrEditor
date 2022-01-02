using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using HocrEditor.Models;

namespace HocrEditor.ViewModels
{
    public class HocrDocumentViewModel : ViewModelBase
    {
        public Dictionary<string, HocrNodeViewModel> NodeCache { get; }

        public ObservableCollection<HocrNodeViewModel> Nodes { get; }

        public ObservableCollection<HocrNodeViewModel> NodeTree { get; }
        public ObservableCollection<HocrNodeViewModel> SelectedNodes { get; }

        public HocrDocumentViewModel(HocrDocument hocrDocument)
        {
            NodeCache = hocrDocument.Items.Select(node => new HocrNodeViewModel(node)).ToDictionary(node => node.Id);

            Nodes = new ObservableCollection<HocrNodeViewModel>(NodeCache.Values);

            var treeNodes = new ObservableCollection<HocrNodeViewModel>();

            foreach (var (_, value) in NodeCache)
            {
                if (string.IsNullOrEmpty(value.ParentId))
                {
                    value.IsRoot = true;

                    treeNodes.Add(value);
                }
                else
                {
                    NodeCache[value.ParentId].Children.Add(value);

                }
            }

            NodeTree = treeNodes;

            SelectedNodes = new ObservableCollection<HocrNodeViewModel>();
        }
    }
}
