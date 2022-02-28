using System.Collections.Generic;
using System.Linq;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Helpers;

public static class NodeHelpers
{
    public static Rect CalculateUnionRect(
        IEnumerable<HocrNodeViewModel> selection
    )
    {
        var list = selection.Where(n => !n.BBox.IsEmpty).ToList();

        if (!list.Any())
        {
            return Rect.Empty;
        }

        return list.Skip(1)
            .Aggregate(
                list.First().BBox,
                (rect, node) =>
                {
                    rect.Union(node.BBox);

                    return rect;
                }
            );
    }

    public static HocrNodeViewModel? FindParent<T>(this HocrNodeViewModel node) where T : HocrNode
    {
        var parent = node.Parent;

        while (parent != null && parent.HocrNode.GetType() != typeof(T))
        {
            parent = parent.Parent;
        }

        return parent;
    }

    public static HocrNodeViewModel? FindParent(this HocrNodeViewModel node, HocrNodeType nodeType)
    {
        var parent = node.Parent;

        while (parent != null && parent.NodeType != nodeType)
        {
            parent = parent.Parent;
        }

        return parent;
    }

    public static IList<HocrNodeViewModel> CloneNodeCollection(IEnumerable<HocrNodeViewModel> nodes)
    {
        var result = new List<HocrNodeViewModel>();

        var dictionary = new SortedDictionary<int, HocrNodeViewModel>();

        // Clone each node and all of its descendants to get a snapshot of the selection in its current state.
        foreach (var node in nodes)
        {
            dictionary.Clear();

            foreach (var descendant in node.Descendants.Prepend(node))
            {
                var clone = (HocrNodeViewModel)descendant.Clone();

                // If the dictionary contains the parent, the parent was a part of the selection.
                // When pasting, we only insert the nodes with a non-clone parent, as they bring their entire cloned
                // sub-tree with them.
                // Those inserted nodes are added to their original parent nodes.
                if (dictionary.ContainsKey(clone.ParentId))
                {
                    clone.Parent = dictionary[descendant.ParentId];
                    clone.Parent.Children.Add(clone);
                }

                dictionary.TryAdd(descendant.Id, clone);

                if (descendant == node)
                {
                    result.Add(clone);
                }
            }
        }

        return result;
    }
}
