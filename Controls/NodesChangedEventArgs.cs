using System;
using System.Collections.Generic;
using System.Windows;
using HocrEditor.ViewModels;
using Rect = HocrEditor.Models.Rect;

namespace HocrEditor.Controls;

public class NodesChangedEventArgs : RoutedEventArgs
{
    public IList<NodeChange> Changes { get; }

    public record NodeChange(HocrNodeViewModel Node, Rect NewBounds, Rect OldBounds)
    {
        public readonly HocrNodeViewModel Node = Node;
        public readonly Rect NewBounds = NewBounds;
        public readonly Rect OldBounds = OldBounds;
    }

    public NodesChangedEventArgs(RoutedEvent routedEvent, object source, IList<NodeChange> changes) : base(routedEvent, source)
    {
        Changes = changes;
    }
}
