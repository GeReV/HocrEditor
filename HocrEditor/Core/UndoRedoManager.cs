using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;

namespace HocrEditor.Core;

public sealed class UndoRedoManager
{
    private int batchNumber;

    private List<UndoRedoCommand> batchCommands = new();

    private readonly List<List<UndoRedoCommand>> commands = new();

    public int CurrentIndex { get; private set; } = -1;

    public bool CanUndo => commands.Count > 0 && CurrentIndex >= 0;

    public bool CanRedo => commands.Count > 0 && CurrentIndex < commands.Count - 1;

    public event EventHandler? UndoStackChanged;


    public void BeginBatch()
    {
        batchNumber++;
    }

    public void ExecuteBatch()
    {
        if (batchNumber == 0)
        {
            throw new InvalidOperationException("No batches stored");
        }

        batchNumber--;

        if (batchNumber > 0)
        {
            return;
        }

        ExecuteCommands(batchCommands);

        batchCommands = new List<UndoRedoCommand>();
    }

    public void ExecuteCommand(UndoRedoCommand command) => ExecuteCommands(Enumerable.Repeat(command, 1));

    public void ExecuteCommands(IEnumerable<UndoRedoCommand> commandSet)
    {
        var commandList = commandSet.ToList();

        if (commandList.Count <= 0)
        {
            return;
        }

        if (batchNumber > 0)
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

        var commandSet = commands[CurrentIndex];

        for (var index = commandSet.Count - 1; index >= 0; index--)
        {
            var command = commandSet[index];

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
        {
            commands.RemoveRange(CurrentIndex + 1, commands.Count - CurrentIndex - 1);
        }
    }

    private void OnUndoStackChanged()
    {
        UndoStackChanged?.Invoke(this, EventArgs.Empty);
    }
}
