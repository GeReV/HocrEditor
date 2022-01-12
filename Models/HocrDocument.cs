using System.Collections.Generic;
using HocrEditor.Helpers;
using HocrEditor.Services;

namespace HocrEditor.Models
{
    public class HocrDocument
    {
        public HocrDocument(IHocrNode rootNode)
        {
            RootNode = rootNode;
        }

        public IHocrNode RootNode { get; }

        public IEnumerable<IHocrNode> Items => RootNode.ChildNodes.RecursiveSelect(n => n.ChildNodes);
    }
}
