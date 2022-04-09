using System.Collections.Generic;
using System.Linq;
using HocrEditor.ViewModels;
using Optional;
using Optional.Collections;

namespace HocrEditor.Helpers;

public static class SelectionHelper
{
    public static Option<HocrNodeViewModel> SelectEditable(IEnumerable<HocrNodeViewModel> items)
    {
        var list = items.ToList();

        var editableNode = list.FirstOrNone();

        if (editableNode.HasValue)
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
                return Option.Some(iter);
            }
        }

        return Option.None<HocrNodeViewModel>();
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
