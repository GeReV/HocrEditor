using System.Collections.ObjectModel;

namespace HocrEditor.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        public bool AutoCrop { get; set; } = true;

        // Workaround for MultiSelectTreeView not working with Document.SelectedNodes directly.
        public ObservableCollection<HocrNodeViewModel>? SelectedItems
        {
            get => Document?.SelectedNodes;
            set
            {
                if (Document != null && value != null)
                {
                    Document.SelectedNodes = new RangeObservableCollection<HocrNodeViewModel>(value);
                }
            }
        }

        public HocrDocumentViewModel? Document { get; set; }
    }
}
