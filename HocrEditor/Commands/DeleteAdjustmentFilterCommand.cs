using HocrEditor.Helpers;
using HocrEditor.ViewModels;
using HocrEditor.ViewModels.Filters;

namespace HocrEditor.Commands;

public class DeleteAdjustmentFilterCommand(HocrPageViewModel hocrPageViewModel)
    : UndoableCommandBase<ImageFilterBase>(hocrPageViewModel)
{
    public override bool CanExecute(ImageFilterBase? filter) => filter is not null;

    public override void Execute(ImageFilterBase? filter)
    {
        if (filter is null)
        {
            return;
        }

        UndoRedoManager.ExecuteCommand(hocrPageViewModel.AdjustmentFilters.ToCollectionRemoveCommand(filter));
    }
}
