// Adapted from: https://gist.github.com/SlyZ/ca7b03931412115cc5fb1416180ad1b4
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using PropertyChanged;

namespace HocrEditor.Core;

/// <summary>
/// A specialised collection used by the ForwardingCommand.Bindings property as a storage for the command mappings.
/// This collection automatically adds and removes its own ForwardingCommandBindings to the
/// <see cref="System.Windows.UIElement.CommandBindings"/> collection of the UIElement to which ForwardingCommand.Bindings is attached.
/// </summary>
[DoNotNotify]
public sealed class ForwardingCommandBindingCollection : FreezableCollection<ForwardingCommandBinding>
{
    private UIElement uiElement;

    /// <summary>
    /// Initializes a new instance of the <see cref="ForwardingCommandBindingCollection"/> class.
    /// </summary>
    /// <param name="uiElement">UIElement to which this collection should add command bindings.</param>
    internal ForwardingCommandBindingCollection(UIElement uiElement)
    {
        this.uiElement = uiElement;
        Hook();
    }

    private void Hook()
    {
        ((INotifyCollectionChanged)this).CollectionChanged += OnCollectionChanged;
    }

    internal void Unhook()
    {
        ((INotifyCollectionChanged)this).CollectionChanged -= OnCollectionChanged;

        for (var i = 0; i < Count; ++i)
        {
            uiElement.CommandBindings.Remove(this.ElementAt(i).CommandBinding);
        }
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (ForwardingCommandBinding binding in e.OldItems)
            {
                uiElement.CommandBindings.Remove(binding.CommandBinding);
            }
        }

        if (e.NewItems != null)
        {
            foreach (ForwardingCommandBinding binding in e.NewItems)
            {
                uiElement.CommandBindings.Add(binding.CommandBinding);
            }
        }
    }
}
