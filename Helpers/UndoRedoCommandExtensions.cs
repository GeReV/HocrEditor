using System.Collections;
using System.Collections.Generic;
using HocrEditor.Commands.UndoRedo;

namespace HocrEditor.Helpers;

public static class UndoRedoCommandExtensions
{
    #region CollectionAddCommand

    public static CollectionAddCommand ToCollectionAddCommand<TSource, TRet>(
        this IList<TSource> obj,
        TRet item
    ) where TRet : notnull =>
        new((IList)obj, item);

    #endregion

    #region CollectionRemoveCommand

    public static CollectionRemoveCommand ToCollectionRemoveCommand<TSource, TRet>(this IList<TSource> obj, TRet item)
        where TRet : notnull => new((IList)obj, item);

    #endregion

    #region CollectionClearCommand

    public static CollectionClearCommand ToCollectionClearCommand(this IList obj) => new(obj);

    #endregion
}
