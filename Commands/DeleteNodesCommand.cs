using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
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

        var page = mainWindowViewModel.Document.CurrentPage ?? throw new InvalidOperationException(
            $"Expected {nameof(mainWindowViewModel.Document.CurrentPage)} to not be null"
        );

        var commands = new List<UndoRedoCommand>
        {
            new PageRemoveNodesCommand(mainWindowViewModel.Document.CurrentPage, nodes)
        };

        if (mainWindowViewModel.AutoClean)
        {
            foreach (var node in nodes)
            {
                if (node.Parent == null)
                {
                    continue;
                }

                commands.AddRange(NodeCommands.CropParents(node.Parent));
                commands.Add(NodeCommands.RemoveEmptyParents(mainWindowViewModel.Document.CurrentPage, node.Parent));
            }
        }

        commands.AddRange(
            nodes.Select(
                selectedNode => PropertyChangeCommand.FromProperty(selectedNode, n => n.IsSelected, false)
            )
        );

        // SelectedNodes.Clear();
        commands.Add(mainWindowViewModel.Document.CurrentPage.SelectedNodes.ToCollectionClearCommand());

        // ExecuteUndoableCommand(commands);
        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }
}
