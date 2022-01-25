using System.Collections.Generic;
using System.Linq;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class AppendSelectNodesCommand : CommandBase<IEnumerable<HocrNodeViewModel>>
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public AppendSelectNodesCommand(MainWindowViewModel mainWindowViewModel)
    {
        this.mainWindowViewModel = mainWindowViewModel;
    }

    public override bool CanExecute(IEnumerable<HocrNodeViewModel>? nodes) =>
        mainWindowViewModel.Document.CurrentPage != null &&
        mainWindowViewModel.Document.CurrentPage.Nodes
            .Any() && nodes != null &&
        nodes.Any();

    public override void Execute(IEnumerable<HocrNodeViewModel>? nodes)
    {
        if (mainWindowViewModel.Document.CurrentPage == null || nodes == null)
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

            commands.Add(mainWindowViewModel.Document.CurrentPage.SelectedNodes.ToCollectionAddCommand(addedItems));
        }

        mainWindowViewModel.UndoRedoManager.ExecuteCommands(commands);
    }
}
