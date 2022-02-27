using System.Collections.Generic;
using System.Windows;
using HocrEditor.ViewModels;

namespace HocrEditor.Controls;

public class WordSplitEventArgs : RoutedEventArgs
{
    public HocrNodeViewModel Node { get; }
    public int SplitPosition { get; }
    public (string, string) Words { get; }

    public WordSplitEventArgs(RoutedEvent routedEvent, object source, HocrNodeViewModel node, int splitPosition, (string, string) words) : base(routedEvent, source)
    {
        Node = node;
        SplitPosition = splitPosition;
        Words = words;
    }
}
