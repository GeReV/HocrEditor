using System;
using System.Collections;
using System.Collections.Generic;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionRemoveAtCommand : UndoRedoCommand
{
    private readonly int index;
    private object? child;

    public CollectionRemoveAtCommand(IList sender, int index) : base(sender)
    {
        this.index = index;
    }

    public override void Undo()
    {
        var list = (IList)Sender;

        list.Insert(index, child ?? throw new InvalidOperationException("Expected child to not be null"));
    }

    public override void Redo()
    {
        var list = (IList)Sender;

        child = list[index];

        list.RemoveAt(index);
    }
}
