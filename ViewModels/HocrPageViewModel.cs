using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands;
using HocrEditor.Controls;
using HocrEditor.Core;
using HocrEditor.Models;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.ViewModels
{
    public partial class HocrPageViewModel : ViewModelBase
    {
        private HocrPage? hocrPage;

        public HocrPage? HocrPage
        {
            get => hocrPage;
            set
            {
                Ensure.IsNotNull(nameof(value), value);

                hocrPage = value;

                NodeCache = BuildNodeCache(value?.Items.Prepend(value) ?? Enumerable.Empty<IHocrNode>());

                Nodes = new RangeObservableCollection<HocrNodeViewModel>(NodeCache.Values);
            }
        }

        public Dictionary<string, HocrNodeViewModel> NodeCache { get; private set; } = new();

        public RangeObservableCollection<HocrNodeViewModel> Nodes { get; private set; } = new();

        public ObservableHashSet<HocrNodeViewModel> SelectedNodes { get; set; } = new();

        public bool IsProcessing => HocrPage == null;

        public string Image { get; set; }

        public HocrPageViewModel(string image)
        {
            Image = image;

            DeleteCommand = new DeleteNodes(this);
            MergeCommand = new MergeNodes(this);
            CropCommand = new CropNodes(this);
            ConvertToImageCommand = new ConvertToImageCommand(this);
            MoveNodesCommand = new MoveNodesCommand(this);
            EditNodesCommand = new RelayCommand<string>(EditNodes, CanEditNodes);

            ExclusiveSelectNodesCommand = new ExclusiveSelectNodesCommand(this);
            AppendSelectNodesCommand = new AppendSelectNodesCommand(this);
            DeselectNodesCommand = new DeselectNodesCommand(this);

            SelectIdenticalNodesCommand =
                new RelayCommand<ICollection<HocrNodeViewModel>>(SelectIdenticalNodes, CanSelectIdenticalNodes);

            UpdateNodesCommand = new RelayCommand<List<NodesChangedEventArgs.NodeChange>>(UpdateNodes, CanUpdateNodes);

            UndoCommand = new RelayCommand(UndoRedoManager.Undo, CanUndo);
            RedoCommand = new RelayCommand(UndoRedoManager.Redo, CanRedo);

            SelectedNodes.CollectionChanged += HandleSelectedNodesChanged;

            UndoRedoManager.UndoStackChanged += UpdateUndoRedoCommands;
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
