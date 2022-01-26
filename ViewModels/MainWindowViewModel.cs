using HocrEditor.Core;

namespace HocrEditor.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        public bool AutoClean
        {
            get => Settings.AutoClean;
            set => Settings.AutoClean = value;
        }

        public HocrDocumentViewModel Document { get; set; } = new();
    }
}
