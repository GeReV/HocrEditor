using System.Collections.Generic;
using System.Linq;
using GongSolutions.Wpf.DragDrop.Utilities;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Controls;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;
using HocrEditor.ViewModels.Filters;

namespace HocrEditor.Commands;

public class MoveAdjustmentFiltersCommand(HocrPageViewModel hocrPageViewModel)
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
        var sourceList = e.SourceCollection.TryGetList();
        var destinationList = e.TargetCollection.TryGetList();
        var data = e.Data.OfType<ImageFilterBase>().ToList();
        var isSameCollection = sourceList.IsSameObservableCollection(destinationList);

        var commands = new List<UndoRedoCommand>();

        if (sourceList is not null && !isSameCollection)
        {
            foreach (var filter in data)
            {
                var index = sourceList.IndexOf(filter);
                if (index == -1)
                {
                    continue;
                }

                commands.Add(new CollectionRemoveCommand(sourceList, filter));

                // If source is destination too, fix the insertion index
                if (destinationList != null && ReferenceEquals(sourceList, destinationList) &&
                    index < insertIndex)
                {
                    --insertIndex;
                }
            }
        }

        if (destinationList is null)
        {
            return;
        }

        if (isSameCollection)
        {
            // Don't register the moving of a single item to the undo stack.
            if (destinationList.Count == 1)
            {
                return;
            }

            foreach (var node in data)
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
        }
        else
        {
            commands.AddRange(
                data.Select(node => destinationList.ToCollectionInsertCommand(insertIndex++, node))
            );
        }

        UndoRedoManager.ExecuteCommands(commands);
    }
}
