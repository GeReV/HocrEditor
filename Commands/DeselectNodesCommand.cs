using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class DeselectNodesCommand : CommandBase<IEnumerable<HocrNodeViewModel>>
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public DeselectNodesCommand(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
    }

    public override bool CanExecute(IEnumerable<HocrNodeViewModel>? nodes) =>
        mainWindowViewModel.Document != null &&
        mainWindowViewModel.Document.SelectedNodes.Any() && nodes != null &&
        nodes.Any();

    public override void Execute(IEnumerable<HocrNodeViewModel>? nodes)
    {
        if (mainWindowViewModel.Document == null)
        {
            return;
        }

        var commands = new List<UndoRedoCommand>();

        var removedItems = nodes?.ToList() ?? mainWindowViewModel.Document.SelectedNodes.ToList();

        if (removedItems.Any())
        {
            commands.AddRange(
                removedItems.Select(
                    node => PropertyChangeCommand.FromProperty(node, n => n.IsSelected, false)
                )
            );

            commands.Add(mainWindowViewModel.Document.SelectedNodes.ToCollectionRemoveCommand(removedItems));
        }

        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }
}
