using System.Collections.Generic;
using System.Windows;
using HocrEditor.ViewModels;

namespace HocrEditor.Controls;

public class NodesEditedEventArgs : RoutedEventArgs
{
    public IEnumerable<HocrNodeViewModel> Nodes { get; }
    public string Value { get; }

    public NodesEditedEventArgs(RoutedEvent routedEvent, object source, IEnumerable<HocrNodeViewModel> nodes, string value) : base(routedEvent, source)
    {
        Nodes = nodes;
        Value = value;
    }
}
