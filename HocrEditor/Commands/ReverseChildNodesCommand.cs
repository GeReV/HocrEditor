using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class ReverseChildNodesCommand(IUndoRedoCommandsService undoRedoCommandsService)
    : UndoableCommandBase<ICollection<HocrNodeViewModel>>(undoRedoCommandsService)
{
    public override bool CanExecute(ICollection<HocrNodeViewModel>? nodes) => nodes is { Count: > 0 };

    public override void Execute(ICollection<HocrNodeViewModel>? nodes)
    {
        if (nodes is not { Count: > 0 })
        {
            return;
        }

        var commands = new List<UndoRedoCommand>();

        foreach (var node in nodes)
        {
            commands.AddRange(
                node.Children.Select(
                    (_, i) => new ObservableCollectionMoveCommand<HocrNodeViewModel>(
                        node.Children,
                        0,
                        node.Children.Count - i - 1
                    )
                )
            );
        }

        UndoRedoManager.ExecuteCommands(commands);
    }
}
