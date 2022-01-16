using System.Collections;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionRemoveCommand : UndoRedoCommand
{
    private readonly IList children;

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public CollectionRemoveCommand(IList sender, object child) : base(sender)
    {
        children = child switch
        {
            IList list => list,
            _ => new ArrayList { child }
        };
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
