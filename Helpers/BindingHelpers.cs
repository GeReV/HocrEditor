using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace HocrEditor.Helpers;

public static class BindingHelpers
{
    public static void SubscribeItemPropertyChanged<T>(
        this ObservableCollection<T> collection,
        PropertyChangedEventHandler handler
    )
        where T : INotifyPropertyChanged
    {
        void CollectionOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var oldItem in e.OldItems)
                {
                    ((INotifyPropertyChanged)oldItem).PropertyChanged -= ItemOnPropertyChanged;
                }
            }

            if (e.NewItems != null)
            {
                foreach (var newItem in e.NewItems)
                {
                    ((INotifyPropertyChanged)newItem).PropertyChanged += ItemOnPropertyChanged;
                }
            }
        }

        void ItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            handler.Invoke(sender, e);
        }

        collection.CollectionChanged += CollectionOnCollectionChanged;

        foreach (var item in collection)
        {
            item.PropertyChanged += ItemOnPropertyChanged;
        }
    }
}
