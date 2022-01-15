using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class DeleteNodes : CommandBase<IList<HocrNodeViewModel>>
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public DeleteNodes(MainWindowViewModel mainWindowViewModel)
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

        var commands = new List<UndoRedoCommand>
        {
            new DocumentRemoveNodesCommand(mainWindowViewModel.Document, nodes)
        };

        if (mainWindowViewModel.AutoCrop)
        {
            foreach (var node in nodes)
            {
                commands.AddRange(CropParents(node));
            }
        }

        commands.AddRange(
            nodes.Select(
                selectedNode => new PropertyChangedCommand(
                    selectedNode,
                    nameof(selectedNode.IsSelected),
                    selectedNode.IsSelected,
                    false
                )
            )
        );

        // SelectedNodes.Clear();
        commands.Add(new CollectionClearCommand(mainWindowViewModel.Document.SelectedNodes));

        // ExecuteUndoableCommand(commands);
        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }

    private static IEnumerable<PropertyChangedCommand> CropParents(HocrNodeViewModel node)
    {
        Debug.Assert(node.Parent != null, "node.Parent != null");

        var ascendants = node.Ascendants.Where(n => n.NodeType != HocrNodeType.Page);

        return ascendants.Select(
            parent => new PropertyChangedCommand(
                parent,
                nameof(parent.BBox),
                parent.BBox,
                NodeHelpers.CalculateUnionRect(parent.Children)
            )
        );
    }
}
