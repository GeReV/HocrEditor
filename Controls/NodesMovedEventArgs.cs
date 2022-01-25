using System.Collections;
using System.Collections.Generic;
using System.Windows;

namespace HocrEditor.Controls;

public class NodesMovedEventArgs : RoutedEventArgs
{
    public IEnumerable SourceCollection { get; }
    public IEnumerable TargetCollection { get; }
    public IList<object> Data { get; }
    public object TargetOwner { get; }
    public int InsertIndex { get; }

    public NodesMovedEventArgs(RoutedEvent id, object source, IEnumerable sourceCollection, IEnumerable targetCollection, IList<object> data, object targetOwner, int insertIndex) : base(id, source)
    {
        SourceCollection = sourceCollection;
        TargetCollection = targetCollection;
        Data = data;
        TargetOwner = targetOwner;
        InsertIndex = insertIndex;
    }
}
