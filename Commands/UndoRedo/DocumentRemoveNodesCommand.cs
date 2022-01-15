using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands.UndoRedo;

public class DocumentRemoveNodesCommand : UndoRedoCommand
{
    private readonly List<HocrNodeViewModel> nodes;
    private List<HocrNodeViewModel> children = new();

    public DocumentRemoveNodesCommand(HocrDocumentViewModel sender, IEnumerable<HocrNodeViewModel> nodes) : base(sender)
    {
        this.nodes = nodes.ToList();
    }

    public override void Undo()
    {
        var document = (HocrDocumentViewModel)Sender;

        foreach (var node in nodes)
        {
            node.Parent?.Children.Add(node);
        }

        foreach (var child in children)
        {
            document.NodeCache.Add(child.Id, child);
        }

        document.Nodes.AddRange(nodes.Concat(children));
        document.SelectedNodes.AddRange(nodes);
    }

    public override void Redo()
    {
        var document = (HocrDocumentViewModel)Sender;

        children = nodes.SelectMany(node => node.Descendents).ToList();

        document.SelectedNodes.RemoveRange(nodes);

        Debug.WriteLine(string.Join(' ', nodes.Concat(children).Select(n => n.Id)));

        document.Nodes.RemoveRange(nodes.Concat(children));

        foreach (var child in nodes.Concat(children))
        {
            document.NodeCache.Remove(child.Id);
        }

        foreach (var node in nodes)
        {
            node.Parent?.Children.Remove(node);
        }
    }
}
