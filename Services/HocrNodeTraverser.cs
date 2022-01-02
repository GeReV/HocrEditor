using System.Collections.Generic;
using HocrEditor.Models;

namespace HocrEditor.Services
{
    public class HocrNodeTraverser
    {
        private readonly IHocrNode node;

        public HocrNodeTraverser(IHocrNode node)
        {
            this.node = node;
        }

        private static void RecurseAppend(ICollection<IHocrNode> list, IHocrNode node)
        {
            list.Add(node);

            foreach (var childNode in node.ChildNodes)
            {
                RecurseAppend(list, childNode);
            }
        }

        public IEnumerable<IHocrNode> ToEnumerable()
        {
            var list = new List<IHocrNode>();

            RecurseAppend(list, node);

            return list;
        }
    }
}
