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
            EditNodesCommand = new RelayCommand<string>(EditNodes, CanEditNodes);

            SelectNodesCommand = new RelayCommand<IEnumerable<HocrNodeViewModel>>(SelectNodes, CanSelectNodes);
            DeselectNodesCommand = new RelayCommand<IEnumerable<HocrNodeViewModel>>(DeselectNodes, CanDeselectNodes);

            SelectIdenticalNodesCommand =
                new RelayCommand<ICollection<HocrNodeViewModel>>(SelectIdenticalNodes, CanSelectIdenticalNodes);

            UpdateNodesCommand = new RelayCommand<List<NodesChangedEventArgs.NodeChange>>(UpdateNodes, CanUpdateNodes);

            UndoCommand = new RelayCommand(UndoRedoManager.Undo, CanUndo);
            RedoCommand = new RelayCommand(UndoRedoManager.Redo, CanRedo);

            PropertyChanged += HandlePropertyChanged;

            UndoRedoManager.UndoStackChanged += UpdateUndoRedoCommands;
        }

        private bool CanSelectIdenticalNodes(ICollection<HocrNodeViewModel>? list) => Document != null && list is { Count: 1 };

        private void SelectIdenticalNodes(ICollection<HocrNodeViewModel>? list)
        {
            if (Document == null || list is not { Count: 1 })
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
                    SelectNodesCommand.TryExecute(
                        Document.Nodes.Where(n => n.NodeType == item.NodeType && n.InnerText == item.InnerText).ToList()
                    );
                    break;
                case HocrNodeType.Image:
                    SelectNodesCommand.TryExecute(
                        Document.Nodes.Where(n => n.NodeType == item.NodeType).ToList()
                    );
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public ConvertToImageCommand ConvertToImageCommand { get; set; }

        public IRelayCommand<ICollection<HocrNodeViewModel>> DeleteCommand { get; }
        public IRelayCommand<ICollection<HocrNodeViewModel>> MergeCommand { get; }
        public IRelayCommand<ICollection<HocrNodeViewModel>> CropCommand { get; init; }
        public IRelayCommand<string> EditNodesCommand { get; }
        public IRelayCommand<IList<HocrNodeViewModel>> SelectNodesCommand { get; }
        public IRelayCommand<IList<HocrNodeViewModel>> DeselectNodesCommand { get; }
        public IRelayCommand<IList<HocrNodeViewModel>> SelectIdenticalNodesCommand { get; }

        public IRelayCommand UndoCommand { get; }
        public IRelayCommand RedoCommand { get; }
        public IRelayCommand<List<NodesChangedEventArgs.NodeChange>> UpdateNodesCommand { get; }

        private void HandlePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Document):
                    if (previousSelectedNodes != null)
                    {
                        previousSelectedNodes.CollectionChanged -= HandleSelectedNodesChanged;
                    }

                    if (Document != null)
                    {
                        Document.SelectedNodes.CollectionChanged += HandleSelectedNodesChanged;

                        previousSelectedNodes = Document.SelectedNodes;
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
            SelectIdenticalNodesCommand.NotifyCanExecuteChanged();
        }

        private bool CanRedo() => UndoRedoManager.CanRedo;

        private bool CanUndo() => UndoRedoManager.CanUndo;

        private bool CanSelectNodes(IEnumerable<HocrNodeViewModel>? nodes) =>
            Document != null && Document.Nodes.Any() && nodes != null && nodes.Any();

        private void SelectNodes(IEnumerable<HocrNodeViewModel>? nodes)
        {
            if (Document == null || nodes == null)
            {
                return;
            }

            var commands = new List<UndoRedoCommand>();

            var addedItems = nodes.ToList();

            if (addedItems.Any())
            {
                commands.Add(Document.SelectedNodes.ToCollectionClearCommand());

                commands.Add(Document.SelectedNodes.ToCollectionAddCommand(addedItems));

                commands.AddRange(
                    addedItems.Select(
                        node => PropertyChangeCommand.FromProperty(node, n => n.IsSelected, true)
                    )
                );
            }

            UndoRedoManager.ExecuteCommands(commands);
        }

        private bool CanDeselectNodes(IEnumerable<HocrNodeViewModel>? nodes) =>
            Document != null && Document.SelectedNodes.Any() && nodes != null && nodes.Any();

        private void DeselectNodes(IEnumerable<HocrNodeViewModel>? nodes)
        {
            if (Document == null || nodes == null)
            {
                return;
            }

            var commands = new List<UndoRedoCommand>();

            var removedItems = nodes.ToList();

            if (removedItems.Any())
            {
                commands.Add(Document.SelectedNodes.ToCollectionRemoveCommand(removedItems));

                commands.AddRange(
                    removedItems.Select(
                        node => PropertyChangeCommand.FromProperty(node, n => n.IsSelected, false)
                    )
                );
            }

            UndoRedoManager.ExecuteCommands(commands);
        }

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
            Document?.SelectedNodes is { Count: > 0 } && Document.SelectedNodes.Any(n => n.IsEditable);

        private void EditNodes(string? value)
        {
            if (Document?.SelectedNodes is not { Count: > 0 } || !Document.SelectedNodes.Any(n => n.IsEditable))
            {
                return;
            }

            var commands = Document.SelectedNodes.Where(node => node.IsEditable)
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
