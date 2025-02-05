using HocrEditor.Helpers;
using HocrEditor.ViewModels;

namespace HocrEditor.Commands;

public class CreateAdjustmentFilterCommand(HocrPageViewModel hocrPageViewModel)
    : UndoableCommandBase<IAdjustmentFilterType>(hocrPageViewModel)
{
    public override bool CanExecute(IAdjustmentFilterType? filterType) => filterType is not null;

    public override void Execute(IAdjustmentFilterType? filterType)
    {
        if (filterType is null)
        {
            return;
        }

        var filter = filterType.Create();

        UndoRedoManager.ExecuteCommand(hocrPageViewModel.AdjustmentFilters.ToCollectionAddCommand(filter));
    }
}
