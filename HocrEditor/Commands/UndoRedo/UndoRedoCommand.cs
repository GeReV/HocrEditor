namespace HocrEditor.Commands.UndoRedo;

public abstract class UndoRedoCommand
{
    public object Sender { get; }

    protected UndoRedoCommand(object sender)
    {
        Sender = sender;
    }

    public abstract void Undo();

    public abstract void Redo();
}
