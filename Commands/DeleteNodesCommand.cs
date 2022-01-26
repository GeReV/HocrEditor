using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Core;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class DeleteNodes : UndoableCommandBase<ICollection<HocrNodeViewModel>>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public DeleteNodes(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
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

        var commands = new List<UndoRedoCommand>
        {
            new PageRemoveNodesCommand(hocrPageViewModel, nodes)
        };


        if (Settings.AutoClean)
        {
            foreach (var node in nodes)
            {
                if (node.Parent == null)
                {
                    continue;
                }

                commands.AddRange(NodeCommands.CropParents(node.Parent));
                commands.Add(NodeCommands.RemoveEmptyParents(hocrPageViewModel, node.Parent));
            }
        }

        commands.AddRange(
            nodes.Select(
                selectedNode => PropertyChangeCommand.FromProperty(selectedNode, n => n.IsSelected, false)
            )
        );

        commands.Add(hocrPageViewModel.SelectedNodes.ToCollectionClearCommand());

        UndoRedoManager.ExecuteCommands(commands);
    }
}
