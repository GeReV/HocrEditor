using System;
using System.Linq.Expressions;

namespace HocrEditor.Commands.UndoRedo;

public static class PropertyChangeCommand
{
    public static PropertyChangeCommand<TRet> FromProperty<TSource, TRet>(
        TSource obj,
        Expression<Func<TSource, TRet>> expression,
        TRet newValue
    ) where TSource : notnull =>
        FromProperty(obj, expression, _ => newValue);

    public static PropertyChangeCommand<TRet> FromProperty<TSource, TRet>(
        TSource obj,
        Expression<Func<TSource, TRet>> expression,
        Func<TRet> newValue
    ) where TSource : notnull =>
        FromProperty(obj, expression, _ => newValue.Invoke());

    public static PropertyChangeCommand<TRet> FromProperty<TSource, TRet>(
        TSource obj,
        Expression<Func<TSource, TRet>> expression,
        Func<TRet, TRet> newValueFunc
    ) where TSource : notnull
    {
        if (expression.Body.NodeType != ExpressionType.MemberAccess)
        {
            throw new InvalidOperationException($"{nameof(expression)} must be a member access");
        }

        var getterFunc = expression.Compile();

        var memberInfo = ((MemberExpression)expression.Body).Member;

        return new PropertyChangeCommand<TRet>(obj, memberInfo.Name, () => getterFunc(obj), newValueFunc);
    }
}

public class PropertyChangeCommand<T> : UndoRedoCommand
{
    private readonly Func<T> oldValueFunc;
    private readonly Func<T, T> newValueFunc;
    public string PropertyName { get; }
    public T OldValue => oldValueFunc();
    public T NewValue => newValueFunc(OldValue);

    public PropertyChangeCommand(object sender, string propertyName, Func<T> oldValueFunc, T newValue) : this(
        sender,
        propertyName,
        oldValueFunc,
        () => newValue
    )
    {
    }

    public PropertyChangeCommand(object sender, string propertyName, Func<T> oldValueFunc, Func<T> newValueFunc) : this(
        sender,
        propertyName,
        oldValueFunc,
        _ => newValueFunc.Invoke()
    )
    {
    }

    public PropertyChangeCommand(object sender, string propertyName, Func<T> oldValueFunc, Func<T, T> newValueFunc) : base(sender)
    {
        PropertyName = propertyName;
        this.oldValueFunc = oldValueFunc;
        this.newValueFunc = newValueFunc;
    }

    public override void Undo()
    {
        var property = Sender.GetType().GetProperty(PropertyName);

        property?.SetValue(Sender, OldValue, null);
    }

    public override void Redo()
    {
        var property = Sender.GetType().GetProperty(PropertyName);

        property?.SetValue(Sender, NewValue, null);
    }
}
