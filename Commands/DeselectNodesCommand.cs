using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class DeselectNodesCommand : UndoableCommandBase<IEnumerable<HocrNodeViewModel>>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public DeselectNodesCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(IEnumerable<HocrNodeViewModel>? nodes) =>
        hocrPageViewModel.SelectedNodes.Any() && nodes != null && nodes.Any();

    public override void Execute(IEnumerable<HocrNodeViewModel>? nodes)
    {
        var commands = new List<UndoRedoCommand>();

        var removedItems = nodes?.ToList() ?? hocrPageViewModel.SelectedNodes.ToList();

        if (removedItems.Any())
        {
            commands.AddRange(
                removedItems.Select(
                    node => PropertyChangeCommand.FromProperty(node, n => n.IsSelected, false)
                )
            );

            commands.Add(
                hocrPageViewModel.SelectedNodes.ToCollectionRemoveCommand(removedItems)
            );
        }

        UndoRedoManager.ExecuteCommands(commands);
    }
}
