using System.Collections;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionRemoveCommand : UndoRedoCommand
{
    private readonly IList children;

    public CollectionRemoveCommand(IList sender, object child) : base(sender)
    {
        children = new ArrayList();
        children.Add(child);
    }

    public CollectionRemoveCommand(IList sender, IList children) : base(sender)
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
