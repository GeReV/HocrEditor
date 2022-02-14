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

public class MoveNodesCommand : UndoableCommandBase<NodesMovedEventArgs>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public MoveNodesCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(NodesMovedEventArgs? e) => e != null;

    public override void Execute(NodesMovedEventArgs? e)
    {
        if (e == null)
        {
            return;
        }

        var insertIndex = e.InsertIndex;
        var destinationList = e.TargetCollection.TryGetList();
        var data = DefaultDropHandler.ExtractData(e.Data).OfType<object>().ToList();
        var isSameCollection = false;

        if (data.TrueForAll(item => IsSameNodeType(item, e.TargetOwner)))
        {
            var list = data.Prepend(e.TargetOwner).Cast<HocrNodeViewModel>().ToList();

            new MergeNodesCommand(hocrPageViewModel).TryExecute(list);

            return;
        }

        var commands = new List<UndoRedoCommand>();

        var sourceList = e.SourceCollection.TryGetList();
        if (sourceList != null)
        {
            isSameCollection = sourceList.IsSameObservableCollection(destinationList);
            if (!isSameCollection)
            {
                foreach (var o in data)
                {
                    var index = sourceList.IndexOf(o);
                    if (index == -1)
                    {
                        continue;
                    }

                    commands.Add(new CollectionRemoveCommand(sourceList, o));

                    // If source is destination too fix the insertion index
                    if (destinationList != null && ReferenceEquals(sourceList, destinationList) &&
                        index < insertIndex)
                    {
                        --insertIndex;
                    }
                }
            }
        }

        if (destinationList == null)
        {
            return;
        }

        foreach (var o in data)
        {
            if (isSameCollection)
            {
                var index = destinationList.IndexOf(o);
                if (index == -1)
                {
                    continue;
                }

                if (insertIndex > index)
                {
                    insertIndex--;
                }

                commands.Add(new CollectionMoveCommand(destinationList, index, insertIndex++));
            }
            else
            {
                commands.Add(new CollectionInsertCommand(destinationList, o, insertIndex++));

                if (o is not HocrNodeViewModel { Parent: { } } node)
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
