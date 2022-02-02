using System.Collections.Generic;

namespace HocrEditor.Models
{
    public class HocrDocument
    {
        public HocrDocument(IEnumerable<HocrPage> pages)
        {
            Pages = new List<HocrPage>(pages);
        }

        public List<HocrPage> Pages { get; }
    }
}
