using System;
using System.Collections.Generic;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Controls;

public class NodesChangedEventArgs : EventArgs
{
    public IList<NodeChange> Changes { get; }

    public record NodeChange(HocrNodeViewModel Node, Rect NewBounds, Rect OldBounds)
    {
        public readonly HocrNodeViewModel Node = Node;
        public readonly Rect NewBounds = NewBounds;
        public readonly Rect OldBounds = OldBounds;
    }

    public NodesChangedEventArgs(IList<NodeChange> changes)
    {
        Changes = changes;
    }
}
