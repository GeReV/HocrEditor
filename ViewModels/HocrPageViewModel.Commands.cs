using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using HocrEditor.Commands;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Controls;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.Models;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.ViewModels
{
    public partial class HocrPageViewModel : IUndoRedoCommandsService
    {
        public UndoRedoManager UndoRedoManager { get; } = new();

        private bool CanSelectIdenticalNodes(ICollection<HocrNodeViewModel>? list) =>
            list is { Count: 1 };

        private void SelectIdenticalNodes(ICollection<HocrNodeViewModel>? list)
        {
            if (list is not { Count: 1 })
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
                        Nodes.Where(n => n.NodeType == item.NodeType && n.InnerText == item.InnerText).ToList()
                    );
                    break;
                case HocrNodeType.Image:
                    ExclusiveSelectNodesCommand.TryExecute(
                        Nodes.Where(n => n.NodeType == item.NodeType).ToList()
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
            SelectedNodes is { Count: > 0 } && SelectedNodes.Any(n => n.IsEditable);

        private void EditNodes(string? value)
        {
            if (SelectedNodes is not { Count: > 0 } || !SelectedNodes.Any(n => n.IsEditable))
            {
                return;
            }

            var commands = SelectedNodes.Where(node => node.IsEditable)
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
