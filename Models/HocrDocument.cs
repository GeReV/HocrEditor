using System.Collections.Generic;
using System.Collections.ObjectModel;
using HocrEditor.Helpers;

namespace HocrEditor.Models
{
    public class HocrDocument
    {
        public HocrDocument(IEnumerable<HocrPage> pages)
        {
            Pages = new ObservableCollection<HocrPage>(pages);
        }

        public ObservableCollection<HocrPage> Pages { get; }
    }
}
