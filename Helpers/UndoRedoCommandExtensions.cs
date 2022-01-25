using System.Collections.Generic;
using HocrEditor.Commands.UndoRedo;

namespace HocrEditor.Helpers;

public static class UndoRedoCommandExtensions
{
    #region CollectionAddCommand

    public static CollectionAddCommand<T> ToCollectionAddCommand<T>(
        this ICollection<T> obj,
        T item
    ) where T : notnull =>
        new(obj, item);

    public static CollectionAddCommand<T> ToCollectionAddCommand<T>(
        this ICollection<T> obj,
        ICollection<T> items
    ) where T : notnull =>
        new(obj, items);

    #endregion

    #region CollectionRemoveCommand

    public static CollectionRemoveCommand<T> ToCollectionRemoveCommand<T>(this ICollection<T> obj, T item)
        where T : notnull => new(obj, item);

    public static CollectionRemoveCommand<T> ToCollectionRemoveCommand<T>(
        this ICollection<T> obj,
        ICollection<T> items
    ) where T : notnull =>
        new(obj, items);

    #endregion

    #region CollectionClearCommand

    public static CollectionClearCommand<T> ToCollectionClearCommand<T>(this ICollection<T> obj) => new(obj);

    #endregion
}
