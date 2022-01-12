namespace HocrEditor.Commands;

public abstract class UndoRedoCommand
{
    public object Sender { get; }

    public UndoRedoCommand(object sender)
    {
        Sender = sender;
    }

    public abstract void Undo();

    public abstract void Redo();
}
