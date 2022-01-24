using System;
using System.Linq;
using System.Windows;
using GongSolutions.Wpf.DragDrop;
using HocrEditor.Behaviors;
using Microsoft.Xaml.Behaviors;


namespace HocrEditor.Controls;

public class DocumentTreeViewDragHandler : DefaultDragHandler
{
    public override void StartDrag(IDragInfo dragInfo)
    {
        var selectionBehavior = Interaction.GetBehaviors(dragInfo.VisualSource)
            .OfType<TreeViewMultipleSelectionBehavior>()
            .FirstOrDefault();

        dragInfo.Data = selectionBehavior?.SelectedItems;

        dragInfo.Effects = dragInfo.Data != null ? DragDropEffects.Copy | DragDropEffects.Move : DragDropEffects.None;
    }
}
