using System.Collections.Generic;

namespace HocrEditor.Models
{
    public class HocrDocument
    {
        public HocrDocument(IEnumerable<HocrPage> pages)
        {
            Pages = new List<HocrPage>(pages);

            DetermineDirection();
        }

        private void DetermineDirection()
        {
            var directionCount = new Dictionary<Direction, int>
            {
                { Direction.Ltr, 0 },
                { Direction.Rtl, 0 },
            };

            foreach (var page in Pages)
            {
                directionCount[page.Direction] += 1;

                foreach (var node in page.Descendants)
                {
                    directionCount[node.Direction] += 1;
                }
            }

            Direction = directionCount[Direction.Ltr] > directionCount[Direction.Rtl] ? Direction.Ltr : Direction.Rtl;
        }

        public List<HocrPage> Pages { get; }

        public Direction Direction { get; set; }

        public string OcrSystem { get; set; } = string.Empty;

        public List<string> Capabilities { get; } = new();
    }
}
