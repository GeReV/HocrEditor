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

    public static HocrNodeViewModel? FindParent<T>(this HocrNodeViewModel node) where T : IHocrNode
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
}
