using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using HocrEditor.Commands;
using HocrEditor.Controls;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using Microsoft.Toolkit.Mvvm.Input;
using Rect = HocrEditor.Models.Rect;

namespace HocrEditor.ViewModels
{
    public partial class HocrPageViewModel : ViewModelBase
    {
        public int LastId { get; set; }
        public HocrPage? HocrPage { get; private set; }

        public RangeObservableCollection<HocrNodeViewModel> Nodes { get; } = new();

        public ObservableHashSet<HocrNodeViewModel> SelectedNodes { get; set; } = new();

        public IEnumerable<HocrNodeViewModel> SelectableNodes => Nodes.Where(n => !n.IsRoot);

        public bool IsProcessing => HocrPage == null;

        public string Image { get; set; }

        public Direction Direction { get; set; }

        public FlowDirection FlowDirection =>
            Direction == Direction.Ltr ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

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
            DeleteCommand = new DeleteNodesCommand(this);
            MergeCommand = new MergeNodesCommand(this);
            CropCommand = new CropNodesCommand(this);
            ConvertToImageCommand = new ConvertToImageCommand(this);
            ReverseChildNodesCommand = new ReverseChildNodesCommand(this);
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

            Nodes.CollectionChanged += HandleNodesChanged;
            Nodes.SubscribeItemPropertyChanged(HandleNodePropertyChanged);

            SelectedNodes.CollectionChanged += HandleSelectedNodesChanged;

            UndoRedoManager.UndoStackChanged += UpdateUndoRedoCommands;
        }

        private void HandleNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Make sure any deleted nodes are removed from selection.
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    var oldItems = e.OldItems ?? throw new ArgumentException("e.OldItems");

                    foreach (var item in oldItems.Cast<HocrNodeViewModel>())
                    {
                        foreach (var node in item.Descendants.Prepend(item))
                        {
                            SelectedNodes.Remove(node);
                        }
                    }
                    break;
                case NotifyCollectionChangedAction.Reset:
                    SelectedNodes.Clear();
                    break;
                case NotifyCollectionChangedAction.Move:
                case NotifyCollectionChangedAction.Add:
                    // Ignore.
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void HandleNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(sender);

            var node = (HocrNodeViewModel)sender;

            if (!node.IsChanged)
            {
                return;
            }

            IsChanged = true;
            OnPropertyChanged(nameof(IsChanged));
        }

        public override void MarkAsUnchanged()
        {
            foreach (var node in Nodes)
            {
                node.MarkAsUnchanged();
            }

            base.MarkAsUnchanged();
        }

        public void Build(HocrPage hocrPage)
        {
            HocrPage = hocrPage;

            Image = hocrPage.Image;
            Direction = hocrPage.Direction;

            var nodeCache = BuildNodeCache(HocrPage.Descendants.Prepend(HocrPage));

            Nodes.Clear();
            Nodes.AddRange(nodeCache.Values);
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

        public override void Dispose()
        {
            UndoRedoManager.UndoStackChanged -= UpdateUndoRedoCommands;

            SelectedNodes.CollectionChanged -= HandleSelectedNodesChanged;

            Nodes.CollectionChanged -= HandleNodesChanged;
            Nodes.UnsubscribeItemPropertyChanged(HandleNodePropertyChanged);

            Nodes.Dispose();
        }
    }
}
