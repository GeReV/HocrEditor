using System.ComponentModel;

namespace HocrEditor.ViewModels
{
    public class ViewModelBase : INotifyPropertyChanged
    {
#pragma warning disable CS0067
        public event PropertyChangedEventHandler? PropertyChanged;
#pragma warning restore CS0067
    }
}
