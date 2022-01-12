using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using HocrEditor.Commands;
using HocrEditor.Controls;
using HocrEditor.Models;
using Microsoft.Toolkit.Mvvm.Input;

namespace HocrEditor.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private ObservableCollection<HocrNodeViewModel>? previousSelectedNodes;

        public bool AutoCrop { get; set; } = true;

        // Workaround for MultiSelectTreeView not working with Document.SelectedNodes directly.
        public ObservableCollection<HocrNodeViewModel>? SelectedNodes
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
