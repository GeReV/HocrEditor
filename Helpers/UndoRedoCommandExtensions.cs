using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq.Expressions;
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

    #region PropertyChangedCommand

    public static PropertyChangedCommand<TRet> ToPropertyChangedCommand<TSource, TRet>(
        this TSource obj,
        Expression<Func<TSource, TRet>> expression,
        TRet newValue
    ) where TSource : notnull =>
        ToPropertyChangedCommand(obj, expression, () => newValue);

    public static PropertyChangedCommand<TRet> ToPropertyChangedCommand<TSource, TRet>(
        this TSource obj,
        Expression<Func<TSource, TRet>> expression,
        Func<TRet> newValueFunc
    ) where TSource : notnull
    {
        if (expression.Body.NodeType != ExpressionType.MemberAccess)
        {
            throw new InvalidOperationException($"{nameof(expression)} must be a member access");
        }

        var getterFunc = expression.Compile();

        var memberInfo = ((MemberExpression)expression.Body).Member;

        return new PropertyChangedCommand<TRet>(obj, memberInfo.Name, getterFunc(obj), newValueFunc);
    }

    #endregion
}
