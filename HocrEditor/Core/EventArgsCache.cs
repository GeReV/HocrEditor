﻿using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HocrEditor.Core;

/// <remarks>
/// To be kept outside <see cref="ObservableCollection{T}"/>, since otherwise, a new instance will be created for each generic type used.
/// </remarks>
internal static class EventArgsCache
{
    internal static readonly PropertyChangedEventArgs AnyPropertyChanged = new(string.Empty);

    internal static readonly PropertyChangedEventArgs CountPropertyChanged = new("Count");

    internal static readonly PropertyChangedEventArgs IndexerPropertyChanged = new("Item[]");

    internal static readonly NotifyCollectionChangedEventArgs ResetCollectionChanged =
        new(NotifyCollectionChangedAction.Reset);
}