using System.Collections;

namespace HocrEditor.Commands;

public class CollectionAddCommand : UndoRedoCommand
{
    private readonly IList children;

    public CollectionAddCommand(IList sender, object child) : base(sender)
    {
        children = new ArrayList();
        children.Add(child);
    }

    public CollectionAddCommand(IList sender, IList children) : base(sender)
    {
        this.children = children;
    }

    public override void Undo()
    {
        var list = (IList)Sender;

        foreach (var child in children)
        {
            list.Remove(child);
        }
    }

    public override void Redo()
    {
        var list = (IList)Sender;

        foreach (var child in children)
        {
            list.Add(child);
        }
    }
}
