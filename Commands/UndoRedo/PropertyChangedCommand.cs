using System;

namespace HocrEditor.Commands.UndoRedo;

public class PropertyChangedCommand<T> : UndoRedoCommand
{
    private readonly Func<T> newValueFunc;
    public string PropertyName { get; }
    public T OldValue { get; }
    public T NewValue => newValueFunc();

    public PropertyChangedCommand(object sender, string propertyName, T oldValue, T newValue) : this(
        sender,
        propertyName,
        oldValue,
        () => newValue
    )
    {
    }

    public PropertyChangedCommand(object sender, string propertyName, T oldValue, Func<T> newValueFunc) : base(sender)
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
