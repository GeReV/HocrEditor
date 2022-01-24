using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GongSolutions.Wpf.DragDrop;
using HocrEditor.Behaviors;
using HocrEditor.ViewModels;
using Microsoft.Xaml.Behaviors;


namespace HocrEditor.Controls;

public class DocumentTreeViewDragHandler : DefaultDragHandler
{
    public override void StartDrag(IDragInfo dragInfo)
    {
        var selectionBehavior = Interaction.GetBehaviors(dragInfo.VisualSource)
            .OfType<TreeViewMultipleSelectionBehavior>()
            .FirstOrDefault();

        var selectedItems = selectionBehavior?.SelectedItems;

        // Ensure the dragged item is selected so it's definitely in the collection.
        if (selectedItems != null && !selectedItems.Contains((HocrNodeViewModel)dragInfo.SourceItem))
        {
            selectionBehavior?.SelectSingleItem((TreeViewItem)dragInfo.VisualSourceItem);
        }

        dragInfo.Data = selectedItems;

        dragInfo.Effects = dragInfo.Data != null ? DragDropEffects.Copy | DragDropEffects.Move : DragDropEffects.None;
    }
}
