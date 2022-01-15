using System;

namespace HocrEditor.Commands.UndoRedo;

public class PropertyChangedCommand : UndoRedoCommand
{
    private readonly Func<object?> newValueFunc;
    public string PropertyName { get; }
    public object? OldValue { get; }
    public object? NewValue
    {
        get => newValueFunc();
    }

    public PropertyChangedCommand(object sender, string propertyName, object? oldValue, object? newValue) : this(
        sender,
        propertyName,
        oldValue,
        () => newValue
    )
    {
    }

    public PropertyChangedCommand(object sender, string propertyName, object? oldValue, Func<object?> newValueFunc) : base(sender)
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
