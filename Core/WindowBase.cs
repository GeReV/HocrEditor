using System.Windows;

namespace HocrEditor.Core;

public class WindowBase<T> : Window
{
    protected T ViewModel => (T)DataContext;
}
