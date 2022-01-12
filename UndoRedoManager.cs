using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands;

namespace HocrEditor;

public class UndoRedoManager
{
    private readonly List<List<UndoRedoCommand>> commands = new();

    public int CurrentIndex { get; private set; } = -1;

    public bool CanUndo => CurrentIndex >= 0;

    public bool CanRedo => commands.Count > 0 && CurrentIndex < commands.Count - 1;

    public void ExecuteCommand(UndoRedoCommand command) => ExecuteCommands(Enumerable.Repeat(command, 1));

    public void ExecuteCommands(IEnumerable<UndoRedoCommand> commandSet)
    {
        var commandList = commandSet.ToList();

        if (commandList.Count <= 0)
        {
            return;
        }

        RemoveRedoCommands();

        foreach (var command in commandList)
        {
            command.Redo();
        }

        commands.Add(commandList);

        CurrentIndex++;
    }

    /// <summary>Reverts the last action. </summary>
    /// <returns>A value indicating whether the undo could be performed. </returns>
    public bool Undo()
    {
        if (!CanUndo)
        {
            return false;
        }

        foreach (var command in commands[CurrentIndex])
        {
            command.Undo();
        }

        CurrentIndex--;

        return true;
    }

    /// <summary>Repeats the last reverted action. </summary>
    /// <returns>A value indicating whether the redo could be performed. </returns>
    public bool Redo()
    {
        if (!CanRedo)
        {
            return false;
        }

        CurrentIndex++;

        foreach (var command in commands[CurrentIndex])
        {
            command.Redo();
        }

        return true;
    }

    private void RemoveRedoCommands()
    {
        if (commands.Count > CurrentIndex + 1)
            commands.RemoveRange(CurrentIndex + 1, commands.Count - CurrentIndex - 1);
    }
}
