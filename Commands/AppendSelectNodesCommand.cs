using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class AppendSelectNodesCommand : UndoableCommandBase<IEnumerable<HocrNodeViewModel>>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public AppendSelectNodesCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
    {
        this.hocrPageViewModel = hocrPageViewModel;
    }

    public override bool CanExecute(IEnumerable<HocrNodeViewModel>? nodes) =>
        hocrPageViewModel.Nodes.Any() && nodes != null && nodes.Any();

    public override void Execute(IEnumerable<HocrNodeViewModel>? nodes)
    {
        if (nodes == null)
        {
            return;
        }

        var commands = new List<UndoRedoCommand>();

        var addedItems = nodes.ToList();

        if (addedItems.Any())
        {
            commands.AddRange(
                addedItems.Select(
                    node => PropertyChangeCommand.FromProperty(node, n => n.IsSelected, true)
                )
            );

            commands.Add(hocrPageViewModel.SelectedNodes.ToCollectionAddCommand(addedItems));
        }

        UndoRedoManager.ExecuteCommands(commands);
    }
}
