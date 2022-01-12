using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using HocrEditor.Commands;
using HocrEditor.Controls;
using HocrEditor.Helpers;
using HocrEditor.Models;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.ViewModels
{
    public partial class MainWindowViewModel
    {
        private readonly UndoRedoManager undoRedoManager = new();

        public MainWindowViewModel()
        {
            DeleteCommand = new RelayCommand(DeleteSelectedNodes, CanEdit);
            MergeCommand = new RelayCommand(MergeSelectedNodes, CanEdit);
            CropCommand = new RelayCommand(CropSelectedNodes, CanEdit);

            SelectNodesCommand = new RelayCommand<IList<HocrNodeViewModel>>(SelectNodes, CanSelectNodes);
            DeselectNodesCommand = new RelayCommand<IList<HocrNodeViewModel>>(DeselectNodes, CanDeselectNodes);

            UndoCommand = new RelayCommand(Undo, CanUndo);
            RedoCommand = new RelayCommand(Redo, CanRedo);
            UpdateNodesCommand = new RelayCommand<List<NodesChangedEventArgs.NodeChange>>(UpdateNodes, CanUpdateNodes);

            PropertyChanged += HandlePropertyChanged;
        }

        public IRelayCommand DeleteCommand { get; init; }
        public IRelayCommand MergeCommand { get; init; }
        public IRelayCommand<IList<HocrNodeViewModel>> SelectNodesCommand { get; }
        public IRelayCommand<IList<HocrNodeViewModel>> DeselectNodesCommand { get; }
        public IRelayCommand UndoCommand { get; init; }
        public IRelayCommand RedoCommand { get; init; }

        public IRelayCommand CropCommand { get; init; }

        public IRelayCommand<List<NodesChangedEventArgs.NodeChange>> UpdateNodesCommand { get; init; }

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
        }

        private void Redo()
        {
            undoRedoManager.Redo();

            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private void Undo()
        {
            undoRedoManager.Undo();

            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private bool CanRedo() => undoRedoManager.CanRedo;

        private bool CanUndo() => undoRedoManager.CanUndo;

        private bool CanEdit() => Document?.SelectedNodes.Any() ?? false;

        private bool CanSelectNodes(IList<HocrNodeViewModel>? nodes) =>
            Document != null && Document.Nodes.Any() && nodes != null && nodes.Any();

        private void SelectNodes(IList<HocrNodeViewModel>? nodes)
        {
            if (Document == null || nodes == null)
            {
                return;
            }

            var commands = new List<UndoRedoCommand>();

            var addedItems = nodes.ToList();

            if (addedItems.Any())
            {
                commands.Add(new CollectionAddCommand(Document.SelectedNodes, addedItems));

                commands.AddRange(
                    addedItems.Select(
                        node => new PropertyChangedCommand(node, nameof(node.IsSelected), node.IsSelected, true)
                    )
                );
            }

            ExecuteUndoableCommand(commands);
        }

        private bool CanDeselectNodes(IList<HocrNodeViewModel>? nodes) =>
            Document != null && Document.SelectedNodes.Any() && nodes != null && nodes.Any();

        private void DeselectNodes(IList<HocrNodeViewModel>? nodes)
        {
            if (Document == null || nodes == null)
            {
                return;
            }

            var commands = new List<UndoRedoCommand>();

            var removedItems = nodes.ToList();

            if (removedItems.Any())
            {
                commands.Add(new CollectionRemoveCommand(Document.SelectedNodes, removedItems));

                commands.AddRange(
                    removedItems.Select(
                        node => new PropertyChangedCommand(node, nameof(node.IsSelected), node.IsSelected, false)
                    )
                );
            }

            undoRedoManager.ExecuteCommands(commands);
        }

        private void DeleteSelectedNodes()
        {
            Debug.Assert(Document != null, $"{nameof(Document)} != null");

            var selectedNodes = Document.SelectedNodes.ToList();

            var commands = new List<UndoRedoCommand>
            {
                new DocumentRemoveNodesCommand(Document, selectedNodes)
            };

            if (AutoCrop)
            {
                foreach (var node in selectedNodes)
                {
                    commands.AddRange(CropParents(node));
                }
            }

            foreach (var selectedNode in selectedNodes)
            {
                // selectedNode.IsSelected = false;
                commands.Add(
                    new PropertyChangedCommand(
                        selectedNode,
                        nameof(selectedNode.IsSelected),
                        selectedNode.IsSelected,
                        false
                    )
                );
            }

            // SelectedNodes.Clear();
            commands.Add(new CollectionClearCommand(Document.SelectedNodes));

            ExecuteUndoableCommand(commands);
        }

        private void MergeSelectedNodes()
        {
            Debug.Assert(Document != null, $"{nameof(Document)} != null");

            if (!Document.SelectedNodes.Any())
            {
                return;
            }

            var selectedNodes = Document.SelectedNodes.OrderBy(node => Document.Nodes.IndexOf(node)).ToList();

            // All child nodes will be merged into the first one.
            var first = selectedNodes.First();
            var rest = selectedNodes.Skip(1).ToArray();

            if (rest.Any(node => node.HocrNode.NodeType != first.HocrNode.NodeType))
            {
                // TODO: Show error.
                return;
            }

            var children = rest.SelectMany(node => node.Children).ToList();

            var commands = new List<UndoRedoCommand>();

            foreach (var parent in rest)
            {
                commands.Add(new CollectionClearCommand(parent.Children));
            }

            foreach (var child in children)
            {
                // child.Parent = first;
                commands.Add(new PropertyChangedCommand(child, nameof(child.Parent), child.Parent, first));

                // child.ParentId = first.Id;
                commands.Add(new PropertyChangedCommand(child, nameof(child.ParentId), child.ParentId, first.Id));

                // first.Children.Add(child);
                commands.Add(new CollectionAddCommand(first.Children, child));
            }

            var nodes = first.Descendents.Prepend(first).ToList();

            // first.BBox = NodeHelpers.CalculateUnionRect(nodes);
            commands.Add(
                new PropertyChangedCommand(first, nameof(first.BBox), first.BBox, NodeHelpers.CalculateUnionRect(nodes))
            );

            commands.Add(new DocumentRemoveNodesCommand(Document, rest));

            ExecuteUndoableCommand(commands);
        }

        private void CropSelectedNodes()
        {
            // Order nodes from the latest occurrence (deepest) to earliest, so if a chain of parent-children is selected,
            // the deepest child is cropped, then its parent and so on, bottom-up.
            var selectedNodes = Document?.SelectedNodes.OrderBy(node => -Document.Nodes.IndexOf(node)) ??
                                Enumerable.Empty<HocrNodeViewModel>();

            var commands = selectedNodes.Select(
                node => new PropertyChangedCommand(
                    node,
                    nameof(HocrNodeViewModel.BBox),
                    node.BBox,
                    NodeHelpers.CalculateUnionRect(node.Children)
                )
            );

            ExecuteUndoableCommand(commands);
        }

        private IEnumerable<PropertyChangedCommand> CropParents(HocrNodeViewModel node)
        {
            Debug.Assert(node.Parent != null, "node.Parent != null");

            var ascendants = node.Ascendants.Where(n => n.NodeType != HocrNodeType.Page);

            return ascendants.Select(
                parent => new PropertyChangedCommand(
                    parent,
                    nameof(parent.BBox),
                    parent.BBox,
                    NodeHelpers.CalculateUnionRect(parent.Children)
                )
            );
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
                change => new PropertyChangedCommand(
                    change.Node,
                    nameof(change.Node.BBox),
                    change.OldBounds,
                    change.NewBounds
                )
            );

            ExecuteUndoableCommand(commands);
        }

        private void ExecuteUndoableCommand(IEnumerable<UndoRedoCommand> commands)
        {
            undoRedoManager.ExecuteCommands(commands);

            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }
    }
}
