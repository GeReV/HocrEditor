using System.Collections;
using System.Collections.Generic;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionRemoveCommand : UndoRedoCommand
{
    private readonly ICollection children;

    public CollectionRemoveCommand(ICollection sender, object child) : base(sender)
    {
        children = new ArrayList { child };
    }

    public CollectionRemoveCommand(ICollection sender, ICollection children) : base(sender)
    {
        this.children = children;
    }

    public override void Undo()
    {
        var list = (IList)Sender;

        foreach (var child in children)
        {
            list.Add(child);
        }
    }

    public override void Redo()
    {
        var list = (IList)Sender;

        foreach (var child in children)
        {
            list.Remove(child);
        }
    }
}
