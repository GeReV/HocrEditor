using System;

namespace HocrEditor.Behaviors;

public class TreeViewDropEventArgs : EventArgs
{
    public object SourceData { get; }
    public object TargetData { get; }
    public DropPosition DropPosition { get; }

    public TreeViewDropEventArgs(object sourceData, object targetData, DropPosition dropPosition)
    {
        SourceData = sourceData;
        TargetData = targetData;
        DropPosition = dropPosition;
    }

}
