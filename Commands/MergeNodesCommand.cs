using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
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

        // All child nodes will be merged into the first one.
        var first = selectedNodes.First();
        var rest = selectedNodes.Skip(1).ToArray();

        if (rest.Any(node => node.NodeType != first.NodeType))
        {
            // TODO: Show error.
            return;
        }

        var children = rest.SelectMany(node => node.Children).ToList();

        var commands = new List<UndoRedoCommand>();

        foreach (var parent in rest)
        {
            commands.Add(parent.Children.ToCollectionClearCommand());
        }

        foreach (var child in children)
        {
            // child.Parent = first;
            commands.Add(PropertyChangeCommand.FromProperty(child, c => c.Parent, first));

            // first.Children.Add(child);
            commands.Add(first.Children.ToCollectionAddCommand(child));
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

        var ascendants = first.Ascendants.Prepend(first);

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
