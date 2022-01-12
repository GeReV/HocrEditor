namespace HocrEditor.Commands;

public class PropertyChangedCommand : UndoRedoCommand
{
    public string PropertyName { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }

    public PropertyChangedCommand(object sender, string propertyName, object? oldValue, object? newValue) : base(sender)
    {
        PropertyName = propertyName;
        OldValue = oldValue;
        NewValue = newValue;
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