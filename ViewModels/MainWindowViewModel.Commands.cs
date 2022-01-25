using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using HocrEditor.Commands;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Controls;
using HocrEditor.Helpers;
using HocrEditor.Models;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.ViewModels
{
    public partial class MainWindowViewModel
    {
        public readonly UndoRedoManager UndoRedoManager = new();

        private ObservableHashSet<HocrNodeViewModel>? previousSelectedNodes;

        public MainWindowViewModel()
        {
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

            PropertyChanged += HandlePropertyChanged;

            UndoRedoManager.UndoStackChanged += UpdateUndoRedoCommands;
        }

        private bool CanSelectIdenticalNodes(ICollection<HocrNodeViewModel>? list) =>
            Document.CurrentPage != null && list is { Count: 1 };

        private void SelectIdenticalNodes(ICollection<HocrNodeViewModel>? list)
        {
            if (Document.CurrentPage == null || list is not { Count: 1 })
            {
                return;
            }

            var item = list.First();

            switch (item.NodeType)
            {
                case HocrNodeType.Page:
                case HocrNodeType.ContentArea:
                case HocrNodeType.Paragraph:
                case HocrNodeType.Line:
                case HocrNodeType.TextFloat:
                case HocrNodeType.Caption:
                case HocrNodeType.Word:
                    ExclusiveSelectNodesCommand.TryExecute(
                        Document.CurrentPage.Nodes.Where(n => n.NodeType == item.NodeType && n.InnerText == item.InnerText).ToList()
                    );
                    break;
                case HocrNodeType.Image:
                    ExclusiveSelectNodesCommand.TryExecute(
                        Document.CurrentPage.Nodes.Where(n => n.NodeType == item.NodeType).ToList()
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }


        public IRelayCommand<ICollection<HocrNodeViewModel>> DeleteCommand { get; }
        public IRelayCommand<ICollection<HocrNodeViewModel>> MergeCommand { get; }
        public IRelayCommand<ICollection<HocrNodeViewModel>> CropCommand { get; }
        public ConvertToImageCommand ConvertToImageCommand { get; set; }
        public IRelayCommand<string> EditNodesCommand { get; }
        public IRelayCommand<NodesMovedEventArgs> MoveNodesCommand { get; }

        public IRelayCommand<IList<HocrNodeViewModel>> ExclusiveSelectNodesCommand { get; }
        public IRelayCommand<IList<HocrNodeViewModel>> AppendSelectNodesCommand { get; }
        public IRelayCommand<IList<HocrNodeViewModel>> DeselectNodesCommand { get; }
        public IRelayCommand<IList<HocrNodeViewModel>> SelectIdenticalNodesCommand { get; }

        public IRelayCommand<List<NodesChangedEventArgs.NodeChange>> UpdateNodesCommand { get; }

        public IRelayCommand UndoCommand { get; }
        public IRelayCommand RedoCommand { get; }

        private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Document):
                    if (previousSelectedNodes != null)
                    {
                        previousSelectedNodes.CollectionChanged -= HandleSelectedNodesChanged;
                    }

                    if (Document.CurrentPage != null)
                    {
                        Document.CurrentPage.SelectedNodes.CollectionChanged += HandleSelectedNodesChanged;

                        previousSelectedNodes = Document.CurrentPage.SelectedNodes;
                    }

                    break;
            }
        }

        private void HandleSelectedNodesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            DeleteCommand.NotifyCanExecuteChanged();
            MergeCommand.NotifyCanExecuteChanged();
            CropCommand.NotifyCanExecuteChanged();
            ConvertToImageCommand.NotifyCanExecuteChanged();
            EditNodesCommand.NotifyCanExecuteChanged();
            ExclusiveSelectNodesCommand.NotifyCanExecuteChanged();
            DeselectNodesCommand.NotifyCanExecuteChanged();
            SelectIdenticalNodesCommand.NotifyCanExecuteChanged();
        }

        private bool CanRedo() => UndoRedoManager.CanRedo;

        private bool CanUndo() => UndoRedoManager.CanUndo;

        private static bool CanUpdateNodes(List<NodesChangedEventArgs.NodeChange>? nodeChanges) =>
            nodeChanges is { Count: > 0 };

        private void UpdateNodes(List<NodesChangedEventArgs.NodeChange>? nodeChanges)
        {
            if (nodeChanges == null)
            {
                return;
            }

            var commands = nodeChanges.Select(
                change => PropertyChangeCommand.FromProperty(
                    change.Node,
                    n => n.BBox,
                    change.NewBounds
                )
            );

            UndoRedoManager.ExecuteCommands(commands);
        }

        private bool CanEditNodes(string? _) =>
            Document.CurrentPage?.SelectedNodes is { Count: > 0 } && Document.CurrentPage.SelectedNodes.Any(n => n.IsEditable);

        private void EditNodes(string? value)
        {
            if (Document.CurrentPage?.SelectedNodes is not { Count: > 0 } || !Document.CurrentPage.SelectedNodes.Any(n => n.IsEditable))
            {
                return;
            }

            var commands = Document.CurrentPage.SelectedNodes.Where(node => node.IsEditable)
                .Select(node => PropertyChangeCommand.FromProperty(node, n => n.InnerText, value));

            UndoRedoManager.ExecuteCommands(commands);
        }

        private void UpdateUndoRedoCommands(object? sender, EventArgs eventArgs)
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }
    }
}
