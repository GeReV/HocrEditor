using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HocrEditor.Helpers;

public static class BindingHelpers
{
    private static readonly Dictionary<(INotifyCollectionChanged, PropertyChangedEventHandler),
            NotifyCollectionChangedEventHandler>
        CollectionChangedHandlerDictionary = new();

    public static void SubscribeItemPropertyChanged<TCollection>(
        this TCollection collection,
        PropertyChangedEventHandler handler
    )
        where TCollection : INotifyCollectionChanged, IEnumerable<INotifyPropertyChanged>
    {
        var key = (collection, handler);

        CollectionChangedHandlerDictionary.Add(key, CollectionChangedHandler);

        collection.CollectionChanged += CollectionChangedHandler;

        foreach (var item in collection)
        {
            item.PropertyChanged += handler;
        }

        return;

        void CollectionChangedHandler(object? _, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var oldItem in e.OldItems)
                {
                    ((INotifyPropertyChanged)oldItem).PropertyChanged -= handler;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var newItem in e.NewItems)
                {
                    ((INotifyPropertyChanged)newItem).PropertyChanged += handler;
                }
            }
        }
    }

    public static void UnsubscribeItemPropertyChanged<TCollection>(
        this TCollection collection,
        PropertyChangedEventHandler handler
    )
        where TCollection : INotifyCollectionChanged, IEnumerable<INotifyPropertyChanged>
    {
        var key = (collection, handler);

        var collectionChangedHandler = CollectionChangedHandlerDictionary[key];

        CollectionChangedHandlerDictionary.Remove(key);

        collection.CollectionChanged -= collectionChangedHandler;

        foreach (var item in collection)
        {
            item.PropertyChanged -= handler;
        }
    }
}
