using System.Collections.Generic;
using System.Linq;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands.UndoRedo;

public class DocumentRemoveNodesCommand : UndoRedoCommand
{
    private readonly List<HocrNodeViewModel> nodes;
    private readonly List<int> indices = new();

    private readonly List<HocrNodeViewModel> children = new();

    public DocumentRemoveNodesCommand(HocrDocumentViewModel sender, IEnumerable<HocrNodeViewModel> nodes) : base(sender)
    {
        this.nodes = nodes.ToList();
    }

    public override void Undo()
    {
        if (!nodes.Any())
        {
            return;
        }

        var document = (HocrDocumentViewModel)Sender;

        foreach (var child in children)
        {
            document.NodeCache.Add(child.Id, child);
        }

        document.Nodes.AddRange(nodes.Concat(children));

        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];

            var insertAt = indices[index];

            // node.Parent = node.ParentId == null ? null : document.NodeCache[node.ParentId];
            node.Parent?.Children.Insert(insertAt, node);
        }

        // foreach (var node in nodes)
        // {
        //     document.SelectedNodes.Add(node);
        // }
    }

    public override void Redo()
    {
        if (!nodes.Any())
        {
            return;
        }

        var document = (HocrDocumentViewModel)Sender;

        children.AddRange(
            nodes
                .SelectMany(node => node.Descendents)
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

        foreach (var child in nodes.Concat(children))
        {
            document.NodeCache.Remove(child.Id);
        }

        document.Nodes.RemoveRange(nodes.Concat(children));
    }
}
