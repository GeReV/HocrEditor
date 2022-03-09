using System;
using System.Collections;
using System.Diagnostics;
using GongSolutions.Wpf.DragDrop.Utilities;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands.UndoRedo;

public class CollectionMoveCommand : UndoRedoCommand
{
    private readonly object item;
    private readonly int destinationIndex;

    private int sourceIndex;

    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public CollectionMoveCommand(ICollection sender, object item, int destinationIndex) : base(sender)
    {
        this.item = item;
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
        var list = (IList)Sender;
        sourceIndex = list.IndexOf(item);

        if (sourceIndex == destinationIndex)
        {
            return;
        }

        Move(list, sourceIndex, destinationIndex);
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
