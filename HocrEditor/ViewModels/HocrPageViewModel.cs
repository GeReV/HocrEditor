﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using HocrEditor.Commands;
using HocrEditor.Controls;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using SkiaSharp;
using Rect = HocrEditor.Models.Rect;

namespace HocrEditor.ViewModels
{
    public partial class HocrPageViewModel : ViewModelBase
    {
        private int lastId;

        private static readonly SKBitmapManager ImageCache = new(1024);

        public HocrPage? HocrPage { get; private set; }

        public RangeObservableCollection<HocrNodeViewModel> Nodes { get; } = new();

        public ObservableHashSet<HocrNodeViewModel> SelectedNodes { get; set; } = new();

        public IEnumerable<HocrNodeViewModel> SelectableNodes => Nodes.Where(n => !n.IsRoot);

        public bool IsProcessing { get; set; }

        public string ImageFilename { get; private set; }

        public SKBitmapManager.SKBitmapReference Image =>
            ImageCache.Get(ImageFilename, () => SKBitmap.Decode(ImageFilename));

        public Task<SKBitmap> ThresholdedBitmap =>
            Image.GetBitmap()
                .ContinueWith(bitmap => AdjustmentFilters.GenerateThresholdedImage(bitmap.Result));

        public Direction Direction
        {
            get => HocrPage?.Direction ?? Direction.Ltr;
            set => HocrPage!.Direction = value;
        }

        public FlowDirection FlowDirection =>
            Direction == Direction.Ltr ? FlowDirection.LeftToRight : FlowDirection.RightToLeft;

        public SKMatrix PageTransformation { get; set; } = SKMatrix.Identity;

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

        public ClipboardViewModel Clipboard { get; } = new();

        public AdjustmentFilters AdjustmentFilters { get; } = new();

        public HocrPageViewModel(HocrPage page) :
            this(page.ImageFilename)
        {
            Build(page);
        }

        public HocrPageViewModel(string imageFilename)
        {
            ImageFilename = imageFilename;

            OcrRegionCommand = new OcrRegionCommand(this);
            DeleteCommand = new DeleteNodesCommand(this);
            CopyCommand = new RelayCommand(Copy);
            PasteCommand = new PasteCommand(this);
            MergeCommand = new MergeNodesCommand(this);
            CropCommand = new CropNodesCommand(this);
            ConvertToImageCommand = new ConvertToImageCommand(this);
            ReverseChildNodesCommand = new ReverseChildNodesCommand(this);
            MoveNodesCommand = new MoveNodesCommand(this);
            EditNodesCommand = new RelayCommand<NodesEditedEventArgs>(EditNodes, CanEditNodes);
            WordSplitCommand = new WordSplitCommand(this);
            CreateNodeCommand = new CreateNodeCommand(this);

            CreateAdjustmentFilterCommand = new CreateAdjustmentFilterCommand(this);
            DeleteAdjustmentFilterCommand = new DeleteAdjustmentFilterCommand(this);
            MoveAdjustmentFiltersCommand = new MoveAdjustmentFiltersCommand(this);

            ExclusiveSelectNodesCommand = new ExclusiveSelectNodesCommand(this);
            AppendSelectNodesCommand = new AppendSelectNodesCommand(this);
            DeselectNodesCommand = new DeselectNodesCommand(this);
            CycleSelectionCommand = new CycleSelectionCommand(this);

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
                    if (e.OldItems == null)
                    {
                        break;
                    }

                    foreach (var item in e.OldItems.Cast<HocrNodeViewModel>())
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
                    throw new ArgumentOutOfRangeException("e.Action");
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

        public int NextId() => ++lastId;

        public void Build(HocrPage hocrPage)
        {
            HocrPage = hocrPage;

            ImageFilename = hocrPage.ImageFilename;
            Direction = hocrPage.Direction;

            var nodeCache = BuildNodeCache(HocrPage.Descendants.Prepend(HocrPage));

            Nodes.Clear();
            Nodes.AddRange(nodeCache.Values);
        }

        private Dictionary<int, HocrNodeViewModel> BuildNodeCache(IEnumerable<HocrNode> nodes)
        {
            var dictionary = new Dictionary<int, HocrNodeViewModel>();

            foreach (var node in nodes)
            {
                var hocrNodeViewModel = new HocrNodeViewModel(node);

                dictionary.Add(hocrNodeViewModel.Id, hocrNodeViewModel);

                if (node.Id > lastId)
                {
                    lastId = node.Id;
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

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            UndoRedoManager.UndoStackChanged -= UpdateUndoRedoCommands;

            SelectedNodes.CollectionChanged -= HandleSelectedNodesChanged;

            AdjustmentFilters.Dispose();

            Nodes.CollectionChanged -= HandleNodesChanged;
            Nodes.UnsubscribeItemPropertyChanged(HandleNodePropertyChanged);
            Nodes.Dispose();
        }
    }
}
