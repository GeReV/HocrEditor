using System.Collections.Generic;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionAddCommand<T> : UndoRedoCommand
{
    private readonly ICollection<T> children;

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public CollectionAddCommand(ICollection<T> sender, T child) : base(sender)
    {
        children = new List<T> { child };
    }

    public CollectionAddCommand(ICollection<T> sender, ICollection<T> children) : base(sender)
    {
        this.children = children;
    }

    public override void Undo()
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

    public override void Redo()
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
}
