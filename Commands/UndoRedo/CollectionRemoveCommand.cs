using System.Collections;
using System.Collections.Generic;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionRemoveCommand<T> : UndoRedoCommand
{
    private readonly ICollection<T> children;

    public CollectionRemoveCommand(ICollection<T> sender, T child) : base(sender)
    {
        children = new List<T> { child };
    }

    public CollectionRemoveCommand(ICollection<T> sender, ICollection<T> children) : base(sender)
    {
        this.children = children;
    }

    public override void Undo()
    {
        if (Sender is ISet<T> set)
        {
            foreach (var child in children)
            {
                set.Add(child);
            }
        }
        else
        {
            var list = (IList<T>)Sender;

            foreach (var child in children)
            {
                list.Add(child);
            }
        }
    }

    public override void Redo()
    {
        if (Sender is ISet<T> set)
        {
            foreach (var child in children)
            {
                set.Remove(child);
            }
        }
        else
        {
            var list = (IList<T>)Sender;

            foreach (var child in children)
            {
                list.Remove(child);
            }
        }
    }
}
