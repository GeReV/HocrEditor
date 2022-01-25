using System;
using System.Collections;
using GongSolutions.Wpf.DragDrop.Utilities;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionMoveCommand : UndoRedoCommand
{
    private readonly int sourceIndex;
    private readonly int destinationIndex;

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public CollectionMoveCommand(ICollection sender, int sourceIndex, int destinationIndex) : base(sender)
    {
        this.sourceIndex = sourceIndex;
        this.destinationIndex = destinationIndex;
    }


    public override void Undo()
    {
        if (sourceIndex == destinationIndex)
        {
            return;
        }

        Move((IList)Sender, destinationIndex, sourceIndex);
    }

    public override void Redo()
    {
        if (sourceIndex == destinationIndex)
        {
            return;
        }

        Move((IList)Sender, sourceIndex, destinationIndex);
    }

    private static void Move(IList list, int source, int dest)
    {
        if (!list.IsObservableCollection())
        {
            throw new ArgumentException("ObservableCollection<T> was expected", nameof(list));
        }

        var method = list.GetType().GetMethod("Move", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        _ = method?.Invoke(list, new object[] { source, dest });
    }
}
