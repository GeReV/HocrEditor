using System;
using System.Windows;
using HocrEditor.ViewModels;

namespace HocrEditor.Core;

public abstract class WindowBase<T> : Window, IDisposable where T : ViewModelBase
{
    protected T ViewModel => (T)DataContext;

    protected WindowBase()
    {
        Unloaded += OnUnloaded;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Dispose();
    }

    public virtual void Dispose()
    {
        ViewModel.Dispose();
    }
}
