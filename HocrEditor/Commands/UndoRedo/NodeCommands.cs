using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands.UndoRedo;

public static class NodeCommands
{
    public static PageRemoveNodesCommand RemoveEmptyParents(HocrPageViewModel page, HocrNodeViewModel node)
    {
        var ascendants = node.Ascendants.Prepend(node).TakeWhile(n => n.NodeType != HocrNodeType.Page && n.Children.Count == 1);

        return new PageRemoveNodesCommand(page, ascendants);
    }

    public static IEnumerable<PropertyChangeCommand<Rect>> CropParents(HocrNodeViewModel node)
    {
        var ascendants = node.Ascendants.Prepend(node).Where(n => n.NodeType != HocrNodeType.Page);

        return ascendants.Select(
            parent =>
                PropertyChangeCommand.FromProperty(
                    parent,
                    p => p.BBox,
                    () => NodeHelpers.CalculateUnionRect(parent.Children)
                )
        );
    }
}
