using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class DeleteNodes : CommandBase<ICollection<HocrNodeViewModel>>
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public DeleteNodes(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
    }

    public override bool CanExecute(ICollection<HocrNodeViewModel>? nodes) => nodes is { Count: > 0 };

    public override void Execute(ICollection<HocrNodeViewModel>? nodes)
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
                commands.Add(RemoveEmptyParents(mainWindowViewModel.Document, node));
                commands.AddRange(CropParents(node));
            }
        }

        commands.AddRange(
            nodes.Select(
                selectedNode => PropertyChangeCommand.FromProperty(selectedNode, n => n.IsSelected, false)
            )
        );

        // SelectedNodes.Clear();
        commands.Add(mainWindowViewModel.Document.SelectedNodes.ToCollectionClearCommand());

        // ExecuteUndoableCommand(commands);
        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }

    private static DocumentRemoveNodesCommand RemoveEmptyParents(HocrDocumentViewModel document, HocrNodeViewModel node)
    {
        Debug.Assert(node.Parent != null, "node.Parent != null");

        var ascendants = node.Ascendants.TakeWhile(n => n.NodeType != HocrNodeType.Page && n.Children.Count == 1);

        return new DocumentRemoveNodesCommand(document, ascendants);
    }

    private static IEnumerable<PropertyChangeCommand<Rect>> CropParents(HocrNodeViewModel node)
    {
        Debug.Assert(node.Parent != null, "node.Parent != null");

        var ascendants = node.Ascendants.Where(n => n.NodeType != HocrNodeType.Page);

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
