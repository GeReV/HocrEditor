using System;
using System.Collections.Generic;
using System.Linq;
using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Controls;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class MoveNodesCommand : CommandBase<NodesMovedEventArgs>
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public MoveNodesCommand(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
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

        var document = mainWindowViewModel.Document ??
                       throw new InvalidOperationException("Expected Document to not be null");

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

                if (mainWindowViewModel.AutoClean)
                {
                    commands.AddRange(NodeCommands.CropParents(oldParent));
                    commands.Add(NodeCommands.RemoveEmptyParents(document, oldParent));
                }
            }
        }

        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }
}
