using System.Collections.Generic;
using System.Linq;
using System.Windows.Documents;
using HocrEditor.Commands.UndoRedo;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class ExclusiveSelectNodesCommand : UndoableCommandBase<IEnumerable<HocrNodeViewModel>>
{
    private readonly HocrPageViewModel hocrPageViewModel;

    public ExclusiveSelectNodesCommand(HocrPageViewModel hocrPageViewModel) : base(hocrPageViewModel)
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

        UndoRedoManager.BeginBatch();

        new DeselectNodesCommand(hocrPageViewModel).TryExecute(hocrPageViewModel.SelectedNodes);

        new AppendSelectNodesCommand(hocrPageViewModel).TryExecute(nodes);

        UndoRedoManager.ExecuteBatch();
    }
}
