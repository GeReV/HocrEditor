using System.Collections.Generic;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionClearCommand<T> : UndoRedoCommand
{
    private ICollection<T> copy = new List<T>();

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public CollectionClearCommand(ICollection<T> sender) : base(sender)
    {
    }

    public override void Undo()
    {
        if (Sender is ISet<T> set)
        {
            foreach (var o in copy)
            {
                set.Add(o);
            }
        }
        else
        {

            var list = (IList<T>)Sender;

            foreach (var o in copy)
            {
                list.Add(o);
            }
        }
    }

    public override void Redo()
    {
        var list = (ICollection<T>)Sender;

        copy = new List<T>(list);

        list.Clear();
    }
}
