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

        if (rest.Any(node => node.HocrNode.NodeType != first.HocrNode.NodeType))
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

        commands.Add(new DocumentRemoveNodesCommand(mainWindowViewModel.Document, rest));

        // first.BBox = NodeHelpers.CalculateUnionRect(nodes);
        commands.Add(
            PropertyChangeCommand.FromProperty(
                first,
                f => f.BBox,
                () =>
                {
                    var newNodes = first.Descendents.Prepend(first).ToList();

                    return NodeHelpers.CalculateUnionRect(newNodes);
                }
            )
        );

        // ExecuteUndoableCommand(commands);
        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }
}
