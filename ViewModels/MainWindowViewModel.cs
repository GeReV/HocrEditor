using System.Diagnostics;
using System.Linq;
using HocrEditor.Services;
using Xamarin.Forms.Internals;

namespace HocrEditor.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public HocrDocumentViewModel? Document { get; set; }

        public MainWindowViewModel()
        {
            // this.WhenAnyValue(vm => vm.Document!.SelectedNodes)
            //     .Subscribe(v =>
            //         {
            //             var selectedNodesObservable = v.ObserveCollectionChanges();
            //             var selectedNodesNotEmptyObservable = selectedNodesObservable.Select(_ => v.Count > 0);
            //
            //             var selectedNodesMergeableObservable = selectedNodesObservable.Select(_ => v.Count >= 2).CombineLatest(
            //                     selectedNodesObservable.Select(
            //                         _ =>
            //                         {
            //                             var types = v.Select(node => node.HocrNode.NodeType).Distinct().ToArray();
            //
            //                             return types.Length == 1 &&
            //                                    types[0] is HocrNodeType.ContentArea or HocrNodeType.Paragraph or HocrNodeType.Line;
            //                         }
            //                     ),
            //                     (a, b) => a && b
            //                 )
            //                 .DistinctUntilChanged();
            //
            //
            //         }
            //     );
        }

        private void Delete()
        {
            Debug.Assert(Document != null, nameof(Document) + " != null");

            foreach (var hocrNodeViewModel in Document.SelectedNodes)
            {
                var nodeIds = new HocrNodeTraverser(hocrNodeViewModel.HocrNode)
                    .ToEnumerable()
                    .Select(node => node.Id);

                nodeIds.ForEach(s => Document.NodeCache.Remove(s));
            }
        }

        private void Merge()
        {
            Debug.Assert(Document != null, nameof(Document) + " != null");

            // All child nodes will be merged into the first one.
            var first = Document.SelectedNodes.First();
            var rest = Document.SelectedNodes.Skip(1).ToArray();

            var children = rest.SelectMany(node => node.Children).ToArray();

            foreach (var child in children)
            {
                child.ParentId = first.Id;
            }

            rest.ForEach(node => Document.NodeCache.Remove(node.Id));
        }
    }
}
