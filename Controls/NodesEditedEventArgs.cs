using System.Windows;

namespace HocrEditor.Controls;

public class NodesEditedEventArgs : RoutedEventArgs
{
    public string Value { get; }

    public NodesEditedEventArgs(RoutedEvent routedEvent, object source, string value) : base(routedEvent, source)
    {
        Value = value;
    }
}
