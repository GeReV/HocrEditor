using System.Collections;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionReplaceCommand : UndoRedoCommand
{
    private int index;
    private readonly object oldChild;
    private readonly object newChild;

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public CollectionReplaceCommand(IList sender, object oldChild, object newChild) : base(sender)
    {
        this.oldChild = oldChild;
        this.newChild = newChild;
    }


    public override void Undo()
    {
        var list = (IList)Sender;

        list.RemoveAt(index);
        list.Insert(index, oldChild);
    }

    public override void Redo()
    {
        var list = (IList)Sender;

        index = list.IndexOf(oldChild);

        list.RemoveAt(index);
        list.Insert(index, newChild);
    }
}
