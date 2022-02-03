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
        public int LastId { get; set; }
        public HocrPage? HocrPage { get; private set; }

        public RangeObservableCollection<HocrNodeViewModel> Nodes { get; private set; } = new();

        public ObservableHashSet<HocrNodeViewModel> SelectedNodes { get; set; } = new();

        public bool IsProcessing => HocrPage == null;

        public string Image { get; set; }

        private Rect selectionBounds;

        public Rect SelectionBounds
        {
            get => selectionBounds;
            set
            {
                selectionBounds = value;

                OcrRegionCommand.NotifyCanExecuteChanged();
            }
        }

        public HocrPageViewModel(HocrPage page) : this(page.Image)
        {
            Build(page);
        }

        public HocrPageViewModel(string image)
        {
            Image = image;

            OcrRegionCommand = new OcrRegionCommand(this);
            DeleteCommand = new DeleteNodes(this);
            MergeCommand = new MergeNodes(this);
            CropCommand = new CropNodes(this);
            ConvertToImageCommand = new ConvertToImageCommand(this);
            MoveNodesCommand = new MoveNodesCommand(this);
            EditNodesCommand = new RelayCommand<NodesEditedEventArgs>(EditNodes, CanEditNodes);

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

        public void Build(HocrPage hocrPage)
        {
            HocrPage = hocrPage;

            var nodeCache = BuildNodeCache(HocrPage.Descendants.Prepend(HocrPage));

            Nodes = new RangeObservableCollection<HocrNodeViewModel>(nodeCache.Values);
        }

        private Dictionary<int, HocrNodeViewModel> BuildNodeCache(IEnumerable<IHocrNode> nodes)
        {
            var dictionary = new Dictionary<int, HocrNodeViewModel>();

            foreach (var node in nodes)
            {
                var hocrNodeViewModel = new HocrNodeViewModel(node);

                dictionary.Add(hocrNodeViewModel.Id, hocrNodeViewModel);

                if (node.Id > LastId)
                {
                    LastId = node.Id;
                }

                if (node.ParentId < 0)
                {
                    continue;
                }

                var parent = dictionary[node.ParentId];

                hocrNodeViewModel.Parent = parent;

                parent.Children.Add(hocrNodeViewModel);
            }

            return dictionary;
        }
    }
}
