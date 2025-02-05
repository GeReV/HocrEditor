using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GongSolutions.Wpf.DragDrop;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;


namespace HocrEditor.Controls;

public class DocumentTreeViewDropHandler(DocumentTreeView owner) : DefaultDropHandler
{
    private new static bool CanAcceptData(IDropInfo dropInfo)
    {
        if (!DefaultDropHandler.CanAcceptData(dropInfo))
        {
            return false;
        }

        var data = ExtractData(dropInfo.Data).OfType<HocrNodeViewModel>().ToList();

        if (data.DistinctBy(n => n.NodeType).Skip(1).Any())
        {
            return false;
        }

        if (dropInfo.TargetItem is not HocrNodeViewModel targetItem)
        {
            return false;
        }

        var isDroppingInto = dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter);

        var hocrNodeType = data[0].NodeType;

        if (isDroppingInto)
        {
            return hocrNodeType == targetItem.NodeType || HocrNodeTypeHelper.CanNodeTypeBeChildOf(hocrNodeType, targetItem.NodeType);
        }

        if (dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.BeforeTargetItem) ||
            dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.AfterTargetItem))
        {
            return HocrNodeTypeHelper.CanNodeTypeBeChildOf(hocrNodeType, targetItem.Parent!.NodeType);
        }

        return false;
    }

    public override void DragOver(IDropInfo? dropInfo)
    {
        if (dropInfo == null || !CanAcceptData(dropInfo))
        {
            return;
        }

        dropInfo.Effects = DragDropEffects.Move;

        var isTreeViewItem = dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter) &&
                             dropInfo.VisualTargetItem is TreeViewItem;
        dropInfo.DropTargetAdorner = isTreeViewItem ? DropTargetAdorners.Highlight : DropTargetAdorners.Insert;
    }

    public override void Drop(IDropInfo? dropInfo)
    {
        if (dropInfo?.DragInfo == null)
        {
            return;
        }

        var insertIndex = GetInsertIndex(dropInfo);
        var data = ExtractData(dropInfo.Data).OfType<object>().ToList();

        var isDroppedOnTarget = dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter) &&
                             dropInfo.VisualTargetItem is TreeViewItem;

        var targetOwner = (HocrNodeViewModel)dropInfo.TargetItem;

        if (!isDroppedOnTarget)
        {
            targetOwner = targetOwner.Parent ?? throw new InvalidOperationException($"Items cannot be dropped above or below a page. Expected {nameof(targetOwner.Parent)} to not be null.");
        }

        owner.RaiseEvent(
            new ListItemsMovedEventArgs(
                DocumentTreeView.NodesMovedEvent,
                owner,
                dropInfo.DragInfo.SourceCollection,
                dropInfo.TargetCollection,
                data,
                targetOwner,
                insertIndex
            )
        );
    }
}
