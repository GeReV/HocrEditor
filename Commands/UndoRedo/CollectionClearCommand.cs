using System.Collections;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionClearCommand : UndoRedoCommand
{
    private IList copy = new ArrayList();

    public CollectionClearCommand(IList sender) : base(sender)
    {
    }

    public override void Undo()
    {
        var list = (IList)Sender;

        foreach (var o in copy)
        {
            list.Add(o);
        }
    }

    public override void Redo()
    {
        var list = (IList)Sender;

        copy = new ArrayList(list);

        list.Clear();
    }
}
