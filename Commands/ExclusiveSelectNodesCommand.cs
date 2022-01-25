using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class ExclusiveSelectNodesCommand : CommandBase<IEnumerable<HocrNodeViewModel>>
{
    private readonly MainWindowViewModel mainWindowViewModel;

    public ExclusiveSelectNodesCommand(MainWindowViewModel mainWindowViewModel)
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

        mainWindowViewModel.UndoRedoManager.BeginBatch();

        new DeselectNodesCommand(mainWindowViewModel).TryExecute(mainWindowViewModel.Document.CurrentPage.SelectedNodes);

        new AppendSelectNodesCommand(mainWindowViewModel).TryExecute(nodes);

        mainWindowViewModel.UndoRedoManager.ExecuteBatch();
    }
}
