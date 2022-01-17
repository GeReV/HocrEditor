using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class MergeNodes : CommandBase<IList<HocrNodeViewModel>>
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public MergeNodes(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
    }

    public override bool CanExecute(IList<HocrNodeViewModel>? nodes) => nodes is { Count: > 0 };

    public override void Execute(IList<HocrNodeViewModel>? nodes)
    {
        if (nodes == null)
        {
            return;
        }

        Debug.Assert(mainWindowViewModel.Document != null, $"{nameof(mainWindowViewModel.Document)} != null");

        var selectedNodes = nodes.OrderBy(node => mainWindowViewModel.Document.Nodes.IndexOf(node)).ToList();

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

        commands.Add(new DocumentRemoveNodesCommand(mainWindowViewModel.Document, rest.Concat(emptyAscendants)));

        var ascendants = first.Ascendants.Prepend(first);

        var updateBoundsCommands = ascendants.Select(
            p =>
                PropertyChangeCommand.FromProperty(
                    p,
                    f => f.BBox,
                    () =>
                    {
                        var newNodes = p.Descendents.Prepend(p).ToList();

                        return NodeHelpers.CalculateUnionRect(newNodes);
                    }
                )
        );

        commands.AddRange(updateBoundsCommands);

        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }
}
