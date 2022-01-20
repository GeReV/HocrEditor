using System.Collections.Generic;
using System.Linq;
using HocrEditor.Models;

namespace HocrEditor.ViewModels
{
    public class HocrDocumentViewModel : ViewModelBase
    {
        public Dictionary<string, HocrNodeViewModel> NodeCache { get; }

        public RangeObservableCollection<HocrNodeViewModel> Nodes { get; }

        public RangeObservableCollection<HocrNodeViewModel> SelectedNodes { get; set; } = new();

        public HocrDocumentViewModel(HocrDocument hocrDocument)
        {
            NodeCache = BuildNodeCache(hocrDocument.Items.Prepend(hocrDocument.RootNode));

            Nodes = new RangeObservableCollection<HocrNodeViewModel>(NodeCache.Values);
        }

        private static Dictionary<string, HocrNodeViewModel> BuildNodeCache(IEnumerable<IHocrNode> nodes)
        {
            var dictionary = new Dictionary<string, HocrNodeViewModel>();

            foreach (var node in nodes)
            {
                var hocrNodeViewModel = new HocrNodeViewModel(node);

                dictionary.Add(hocrNodeViewModel.Id, hocrNodeViewModel);

                if (string.IsNullOrEmpty(node.ParentId))
                {
                    hocrNodeViewModel.IsRoot = true;
                }
                else
                {
                    var parent = dictionary[node.ParentId];

                    hocrNodeViewModel.Parent = parent;

                    parent.Children.Add(hocrNodeViewModel);
                }
            }

            return dictionary;
        }
    }
}
