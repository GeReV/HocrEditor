using System.Windows;

namespace HocrEditor.Controls;

public class NodesEditedEventArgs : RoutedEventArgs
{
    public string Value { get; }

    public NodesEditedEventArgs(string value)
    {
        Value = value;
    }
}
