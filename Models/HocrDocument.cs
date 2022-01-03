using System.Collections.Generic;
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

        public IEnumerable<IHocrNode> Items => new HierarchyTraverser<IHocrNode>(node => node.ChildNodes).ToEnumerable(RootNode);
    }
}
