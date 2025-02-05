using System.Linq;
using GongSolutions.Wpf.DragDrop;
using HocrEditor.Controls.Adjustments;

namespace HocrEditor.Controls;

public class AdjustmentFiltersDropHandler(AdjustmentsControl owner) : DefaultDropHandler
{
    public override void Drop(IDropInfo? dropInfo)
    {
        if (dropInfo?.DragInfo == null)
        {
            return;
        }

        var insertIndex = GetInsertIndex(dropInfo);
        var data = ExtractData(dropInfo.Data).OfType<object>().ToList();

        owner.RaiseEvent(
            new ListItemsMovedEventArgs(
                AdjustmentsControl.FiltersMovedEvent,
                owner,
                dropInfo.DragInfo.SourceCollection,
                dropInfo.TargetCollection,
                data,
                dropInfo.TargetItem,
                insertIndex
            )
        );
    }
}
