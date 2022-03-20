using System.Collections.ObjectModel;

namespace HocrEditor.Commands.UndoRedo;

public class ObservableCollectionMoveCommand<T> : UndoRedoCommand
{
    private readonly int oldIndex;
    private readonly int newIndex;

    public ObservableCollectionMoveCommand(ObservableCollection<T> sender, int oldIndex, int newIndex) : base(sender)
    {
        this.oldIndex = oldIndex;
        this.newIndex = newIndex;
    }

    public ObservableCollection<T> Collection => (ObservableCollection<T>)Sender;

    public override void Undo()
    {
        Collection.Move(newIndex, oldIndex);
    }

    public override void Redo()
    {
        Collection.Move(oldIndex, newIndex);
    }
}
