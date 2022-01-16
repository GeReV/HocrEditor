using System;
using System.Linq.Expressions;

namespace HocrEditor.Commands.UndoRedo;

public static class PropertyChangeCommand {
    public static PropertyChangeCommand<TRet> FromProperty<TSource, TRet>(
        TSource obj,
        Expression<Func<TSource, TRet>> expression,
        TRet newValue
    ) where TSource : notnull =>
        FromProperty(obj, expression, () => newValue);

    public static PropertyChangeCommand<TRet> FromProperty<TSource, TRet>(
        TSource obj,
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

        return new PropertyChangeCommand<TRet>(obj, memberInfo.Name, getterFunc(obj), newValueFunc);
    }
}

public class PropertyChangeCommand<T> : UndoRedoCommand
{
    private readonly Func<T> newValueFunc;
    public string PropertyName { get; }
    public T OldValue { get; }
    public T NewValue => newValueFunc();

    public PropertyChangeCommand(object sender, string propertyName, T oldValue, T newValue) : this(
        sender,
        propertyName,
        oldValue,
        () => newValue
    )
    {
    }

    public PropertyChangeCommand(object sender, string propertyName, T oldValue, Func<T> newValueFunc) : base(sender)
    {
        PropertyName = propertyName;
        OldValue = oldValue;
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
