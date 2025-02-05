using System.Collections;
using System.Collections.Generic;
using System.Windows;

namespace HocrEditor.Controls;

public class ListItemsMovedEventArgs(
    RoutedEvent id,
    object source,
    IEnumerable sourceCollection,
    IEnumerable targetCollection,
    IList<object> data,
    object targetOwner,
    int insertIndex
)
    : RoutedEventArgs(id, source)
{
    public IEnumerable SourceCollection { get; } = sourceCollection;
    public IEnumerable TargetCollection { get; } = targetCollection;
    public IList<object> Data { get; } = data;
    public object TargetOwner { get; } = targetOwner;
    public int InsertIndex { get; } = insertIndex;
}
