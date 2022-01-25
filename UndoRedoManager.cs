using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;

namespace HocrEditor;

public class UndoRedoManager
{
    private readonly List<List<UndoRedoCommand>> commands = new();

    public int CurrentIndex { get; private set; } = -1;

    public bool CanUndo => CurrentIndex >= 0;

    public bool CanRedo => commands.Count > 0 && CurrentIndex < commands.Count - 1;

    public event EventHandler? UndoStackChanged;

    private bool isBatching = false;

    private List<UndoRedoCommand> batchCommands = new();

    public void BeginBatch()
    {
        isBatching = true;
    }

    public void ExecuteBatch()
    {
        isBatching = false;

        ExecuteCommands(batchCommands);

        batchCommands = new List<UndoRedoCommand>();
    }

    public void ExecuteCommands(IEnumerable<UndoRedoCommand> commandSet)
    {
        var commandList = commandSet.ToList();

        if (commandList.Count <= 0)
        {
            return;
        }

        if (isBatching)
        {
            batchCommands.AddRange(commandList);

            return;
        }

        RemoveRedoCommands();

        foreach (var command in commandList)
        {
            command.Redo();
        }

        commands.Add(commandList);

        CurrentIndex++;

        OnUndoStackChanged();
    }

    public void Clear()
    {
        commands.Clear();

        CurrentIndex = -1;

        OnUndoStackChanged();
    }

    /// <summary>Reverts the last action. </summary>
    /// <returns>A value indicating whether the undo could be performed. </returns>
    public void Undo()
    {
        if (!CanUndo)
        {
            return;
        }

        foreach (var command in commands[CurrentIndex])
        {
            command.Undo();
        }

        CurrentIndex--;

        OnUndoStackChanged();
    }

    /// <summary>Repeats the last reverted action. </summary>
    /// <returns>A value indicating whether the redo could be performed. </returns>
    public void Redo()
    {
        if (!CanRedo)
        {
            return;
        }

        CurrentIndex++;

        foreach (var command in commands[CurrentIndex])
        {
            command.Redo();
        }

        OnUndoStackChanged();
    }

    private void RemoveRedoCommands()
    {
        if (commands.Count > CurrentIndex + 1)
            commands.RemoveRange(CurrentIndex + 1, commands.Count - CurrentIndex - 1);
    }

    protected virtual void OnUndoStackChanged()
    {
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
    }
}
