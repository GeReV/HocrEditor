using System.Collections.Generic;
using System.Linq;
using HocrEditor.ViewModels;

namespace HocrEditor.Helpers;

public static class SelectionHelper
{
    public static HocrNodeViewModel? SelectEditable(IEnumerable<HocrNodeViewModel> items)
    {
        var list = items.ToList();

        var editableNode = list.FirstOrDefault(n => n.IsEditable);

        if (editableNode != null)
        {
            return editableNode;
        }

        foreach (var node in list)
        {
            var iter = node;
            while (iter.Children.Count == 1 && !iter.IsEditable)
            {
                iter = iter.Children[0];
            }

            if (iter.IsEditable)
            {
                return iter;
            }
        }

        return null;
    }

    public static IEnumerable<HocrNodeViewModel> SelectAllEditable(IEnumerable<HocrNodeViewModel> items)
    {
        var list = items.ToList();

        var nodes = list.Where(n => n.IsEditable).ToList();

        foreach (var node in list.Where(n => !n.IsEditable))
        {
            var iter = node;
            while (iter.Children.Count == 1 && !iter.IsEditable)
            {
                iter = iter.Children[0];
            }

            if (iter.IsEditable)
            {
                nodes.Add(iter);
            }
        }

        return nodes;
    }
}
