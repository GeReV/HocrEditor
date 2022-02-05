using System.Collections.Generic;
using System.Linq;
using System.Text;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class MergeNodes : UndoableCommandBase<ICollection<HocrNodeViewModel>>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public MergeNodes(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(ICollection<HocrNodeViewModel>? nodes) => nodes is { Count: > 0 };

    public override void Execute(ICollection<HocrNodeViewModel>? nodes)
    {
        if (nodes == null)
        {
            return;
        }

        var selectedNodes = nodes.OrderBy(node => hocrPageViewModel.Nodes.IndexOf(node)).ToList();

        if (!selectedNodes.Any())
        {
            return;
        }

        // All child nodes will be merged into the first one, which will be the "host".
        var hostNode = selectedNodes.First();
        var rest = selectedNodes.Skip(1).ToArray();

        if (rest.Any(node => node.NodeType != hostNode.NodeType))
        {
            // TODO: Show error.
            return;
        }


        var commands = new List<UndoRedoCommand>();

        // All the children of these nodes will move into the host node, so we clear all children collections.
        foreach (var parent in rest)
        {
            commands.Add(parent.Children.ToCollectionClearCommand());
        }

        if (hostNode.NodeType == HocrNodeType.Word)
        {
            // Word nodes have no children, but have text that has to be concatenated.
            var sb = new StringBuilder(hostNode.InnerText);

            foreach (var node in rest)
            {
                sb.Append(node.InnerText);
            }

            commands.Add(PropertyChangeCommand.FromProperty(hostNode, n => n.BBox, NodeHelpers.CalculateUnionRect(rest.Prepend(hostNode))));
            commands.Add(PropertyChangeCommand.FromProperty(hostNode, n => n.InnerText, sb.ToString()));
        }
        else
        {
            var children = rest.SelectMany(node => node.Children).ToList();

            // Non-word nodes need to move their children to the new host.
            foreach (var child in children)
            {
                // Set child's new parent to the host node.
                commands.Add(PropertyChangeCommand.FromProperty(child, c => c.Parent, hostNode));

                // Add the child to the host node's children collection.
                commands.Add(hostNode.Children.ToCollectionAddCommand(child));
            }
        }

        // Find all ascendants which will remain empty after the merge, and include them in the deletion.
        var emptyAscendants = rest.SelectMany(
                node =>
                    // Climb the parents and keep the ones who will remain empty after their child is deleted.
                    // If one is encountered that will not remain empty, or we arrive at the root node, stop.
                    node.Ascendants.TakeWhile(a => a.Children.Count == 1 && !a.IsRoot)
            )
            .ToList();

        commands.Add(new PageRemoveNodesCommand(hocrPageViewModel, rest.Concat(emptyAscendants)));

        var ascendants = hostNode.Ascendants.Prepend(hostNode);

        var updateBoundsCommands = ascendants.Select(
            p =>
                PropertyChangeCommand.FromProperty(
                    p,
                    f => f.BBox,
                    () =>
                    {
                        var newNodes = p.Descendants.Prepend(p).ToList();

                        return NodeHelpers.CalculateUnionRect(newNodes);
                    }
                )
        );

        commands.AddRange(updateBoundsCommands);

        UndoRedoManager.ExecuteCommands(commands);
    }
}
