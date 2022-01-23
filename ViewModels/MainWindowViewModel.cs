using System.Collections.ObjectModel;

namespace HocrEditor.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public bool AutoCrop { get; set; } = true;

        public HocrDocumentViewModel? Document { get; set; }
    }
}
