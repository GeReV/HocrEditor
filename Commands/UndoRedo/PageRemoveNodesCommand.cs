using System.Collections.Generic;
using System.Linq;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands.UndoRedo;

public class PageRemoveNodesCommand : UndoRedoCommand
{
    private readonly List<HocrNodeViewModel> nodes;
    private readonly List<int> indices = new();

    private readonly List<HocrNodeViewModel> children = new();

    public PageRemoveNodesCommand(HocrPageViewModel sender, IEnumerable<HocrNodeViewModel> nodes) : base(sender)
    {
        this.nodes = nodes.ToList();
    }

    public override void Undo()
    {
        if (!nodes.Any())
        {
            return;
        }

        var document = (HocrPageViewModel)Sender;

        // NOTE: Ordering is important here. Updating the node array must occur before inserting the children, as
        //  the latter affects selected items, which should update after the nodes.
        document.Nodes.AddRange(nodes.Concat(children));

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];

            var insertAt = indices[index];

            // node.Parent = node.ParentId == null ? null : document.NodeCache[node.ParentId];
            node.Parent?.Children.Insert(insertAt, node);
        }
    }

    public override void Redo()
    {
        if (!nodes.Any())
        {
            return;
        }

        var document = (HocrPageViewModel)Sender;

        children.AddRange(
            nodes
                .SelectMany(node => node.Descendants)
                .Except(nodes)
                .Distinct()
        );

        foreach (var node in nodes)
        {
            document.SelectedNodes.Remove(node);
        }

        indices.Clear();
        indices.AddRange(nodes.Select(n => n.Parent?.Children.IndexOf(n) ?? 0));

        foreach (var node in nodes)
        {
            node.Parent?.Children.Remove(node);
            // TODO: Should this also be cleaned?
            // node.Parent = null;
        }

        document.Nodes.RemoveRange(nodes.Concat(children));
    }
}
