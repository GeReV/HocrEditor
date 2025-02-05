using System.Collections.Generic;
using System.Linq;
using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Controls;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class MoveNodesCommand(HocrPageViewModel hocrPageViewModel)
    : UndoableCommandBase<ListItemsMovedEventArgs>(hocrPageViewModel)
{
    public override bool CanExecute(ListItemsMovedEventArgs? e) => e != null;

    public override void Execute(ListItemsMovedEventArgs? e)
    {
        if (e == null)
        {
            return;
        }

        var insertIndex = e.InsertIndex;
        var destinationList = e.TargetCollection.TryGetList();
        var data = e.Data.OfType<HocrNodeViewModel>().ToList();
        var isSameCollection = false;

        if (data.TrueForAll(item => IsSameNodeType(item, e.TargetOwner)))
        {
            var list = data.Prepend(e.TargetOwner).Cast<HocrNodeViewModel>().ToList();

            new MergeNodesCommand(hocrPageViewModel).TryExecute(list);

            return;
        }

        var commands = new List<UndoRedoCommand>();

        foreach (var node in data)
        {
            var sourceList = node.Parent?.Children;
            if (sourceList == null)
            {
                continue;
            }

            isSameCollection = sourceList.IsSameObservableCollection(destinationList);
            if (isSameCollection)
            {
                continue;
            }

            var index = sourceList.IndexOf(node);
            if (index == -1)
            {
                continue;
            }

            commands.Add(new CollectionRemoveCommand(sourceList, node));

            // If source is destination too, fix the insertion index
            if (destinationList != null && ReferenceEquals(sourceList, destinationList) &&
                index < insertIndex)
            {
                --insertIndex;
            }
        }

        if (destinationList == null)
        {
            return;
        }

        foreach (var node in data)
        {
            if (isSameCollection)
            {
                var index = destinationList.IndexOf(node);
                if (index == -1)
                {
                    continue;
                }

                if (insertIndex > index)
                {
                    insertIndex--;
                }

                commands.Add(new CollectionMoveCommand(destinationList, node, insertIndex++));
            }
            else
            {
                commands.Add(destinationList.ToCollectionInsertCommand(insertIndex++, node));

                if (node.Parent == null)
                {
                    continue;
                }

                var oldParent = node.Parent;

                commands.Add(PropertyChangeCommand.FromProperty(node, n => n.Parent, e.TargetOwner));

                if (Settings.AutoClean)
                {
                    // Crop old parent, to narrow it to its remaining descendants.
                    commands.AddRange(NodeCommands.CropParents(oldParent));

                    // Remove any empty leftover nodes.
                    commands.Add(NodeCommands.RemoveEmptyParents(hocrPageViewModel, oldParent));

                    // Update the bounds for the new owner and its ancestors.
                    commands.AddRange(NodeCommands.CropParents((HocrNodeViewModel)e.TargetOwner));
                }
            }
        }

        UndoRedoManager.ExecuteCommands(commands);
    }

    private static bool IsSameNodeType(object a, object b) => a is HocrNodeViewModel nodeA &&
                                                              b is HocrNodeViewModel nodeB &&
                                                              nodeA.NodeType == nodeB.NodeType;
}
