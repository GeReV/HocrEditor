using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using HocrEditor.Commands;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Controls;
using HocrEditor.Helpers;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.ViewModels
{
    public partial class MainWindowViewModel
    {
        public readonly UndoRedoManager UndoRedoManager = new();

        public MainWindowViewModel()
        {
            DeleteCommand = new DeleteNodes(this);
            MergeCommand = new MergeNodes(this);
            CropCommand = new CropNodes(this);
            ConvertToImageCommand = new ConvertToImageCommand(this);

            SelectNodesCommand = new RelayCommand<IList<HocrNodeViewModel>>(SelectNodes, CanSelectNodes);
            DeselectNodesCommand = new RelayCommand<IList<HocrNodeViewModel>>(DeselectNodes, CanDeselectNodes);

            UndoCommand = new RelayCommand(UndoRedoManager.Undo, CanUndo);
            RedoCommand = new RelayCommand(UndoRedoManager.Redo, CanRedo);
            UpdateNodesCommand = new RelayCommand<List<NodesChangedEventArgs.NodeChange>>(UpdateNodes, CanUpdateNodes);

            PropertyChanged += HandlePropertyChanged;

            UndoRedoManager.UndoStackChanged += UpdateUndoRedoCommands;
        }

        public ConvertToImageCommand ConvertToImageCommand { get; set; }

        public IRelayCommand<IList<HocrNodeViewModel>> DeleteCommand { get; }
        public IRelayCommand<IList<HocrNodeViewModel>> MergeCommand { get; }
        public IRelayCommand<IList<HocrNodeViewModel>> CropCommand { get; init; }
        public IRelayCommand<IList<HocrNodeViewModel>> SelectNodesCommand { get; }
        public IRelayCommand<IList<HocrNodeViewModel>> DeselectNodesCommand { get; }
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
        }

        private bool CanRedo() => UndoRedoManager.CanRedo;

        private bool CanUndo() => UndoRedoManager.CanUndo;

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
                commands.Add(Document.SelectedNodes.ToCollectionAddCommand(addedItems));

                commands.AddRange(
                    addedItems.Select(
                        node => PropertyChangeCommand.FromProperty(node, n => n.IsSelected, true)
                    )
                );
            }

            UndoRedoManager.ExecuteCommands(commands);
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

        private void UpdateUndoRedoCommands(object? sender, EventArgs eventArgs)
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }
    }
}
