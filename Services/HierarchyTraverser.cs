using System;
using System.Collections.Generic;

namespace HocrEditor.Services
{
    public class HierarchyTraverser<T>
    {
        private readonly Func<T, IEnumerable<T>> getChildren;

        public HierarchyTraverser(Func<T, IEnumerable<T>> getChildren)
        {
            this.getChildren = getChildren;
        }

        private void RecurseAppend(ICollection<T> list, T node)
        {
            list.Add(node);

            foreach (var childNode in getChildren(node))
            {
                RecurseAppend(list, childNode);
            }
        }

        public IEnumerable<T> ToEnumerable(T node)
        {
            var list = new List<T>();

            RecurseAppend(list, node);

            return list;
        }
    }
}
