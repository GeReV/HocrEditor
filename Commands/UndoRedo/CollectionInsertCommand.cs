using System.Collections;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionInsertCommand : UndoRedoCommand
{
    private readonly int index;
    private readonly object child;

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public CollectionInsertCommand(ICollection sender, object child, int index) : base(sender)
    {
        this.index = index;
        this.child = child;
    }


    public override void Undo()
    {
        var list = (IList)Sender;

        list.Remove(child);
    }

    public override void Redo()
    {
        var list = (IList)Sender;

        list.Insert(index, child);
    }
}
