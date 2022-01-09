using System.Collections.Generic;
using System.Linq;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor;

public static class NodeHelpers
{
    public static Rect CalculateUnionRect(
        IEnumerable<HocrNodeViewModel> selection
    )
    {
        var list = selection.ToList();

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
}
