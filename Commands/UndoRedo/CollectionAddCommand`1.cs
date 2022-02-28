using System.Collections.Generic;
using System.Reflection;

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

            return;
        }

        var list = (IList<T>)Sender;

        if (children.Count > 1 && TryAddRange(list, children))
        {
            return;
        }

        foreach (var child in children)
        {
            list.Add(child);
        }
    }

    private static bool TryAddRange(IList<T> list, ICollection<T> collection)
    {
        var addRangeMethod = list.GetType().GetMethod("AddRange", BindingFlags.Public | BindingFlags.Instance);

        if (addRangeMethod == null)
        {
            return false;
        }

        addRangeMethod.Invoke(list, new object?[] { collection });

        return true;
    }
}
