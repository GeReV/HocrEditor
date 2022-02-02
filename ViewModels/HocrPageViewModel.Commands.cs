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

        private static bool CanSelectIdenticalNodes(ICollection<HocrNodeViewModel>? list) =>
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


        public IRelayCommand<Rect> OcrRegionCommand { get; }
        public IRelayCommand<ICollection<HocrNodeViewModel>> DeleteCommand { get; }
        public IRelayCommand<ICollection<HocrNodeViewModel>> MergeCommand { get; }
        public IRelayCommand<ICollection<HocrNodeViewModel>> CropCommand { get; }
        public ConvertToImageCommand ConvertToImageCommand { get; set; }
        public IRelayCommand<NodesEditedEventArgs> EditNodesCommand { get; }
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

        private static bool CanEditNodes(NodesEditedEventArgs? e)
        {
            if (e == null)
            {
                return false;
            }

            var list = e.Nodes.ToList();

            return list.Count > 0 && list.All(n => n.IsEditable);
        }

        private void EditNodes(NodesEditedEventArgs? e)
        {
            if (e == null)
            {
                return;
            }

            var list = e.Nodes.ToList();

            if (list.Count <= 0 || !list.All(n => n.IsEditable))
            {
                return;
            }

            var commands = list
                .Select(node => PropertyChangeCommand.FromProperty(node, n => n.InnerText, e.Value));

            UndoRedoManager.ExecuteCommands(commands);
        }

        private void UpdateUndoRedoCommands(object? sender, EventArgs eventArgs)
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }
    }
}
