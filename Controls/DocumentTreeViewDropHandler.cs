using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GongSolutions.Wpf.DragDrop;
using GongSolutions.Wpf.DragDrop.Utilities;
using HocrEditor.Helpers;
using HocrEditor.ViewModels;


namespace HocrEditor.Controls;

public class DocumentTreeViewDropHandler : DefaultDropHandler
{
    private readonly DocumentTreeView owner;

    public DocumentTreeViewDropHandler(DocumentTreeView owner)
    {
        this.owner = owner;
    }

    private new static bool CanAcceptData(IDropInfo dropInfo)
    {
        if (!DefaultDropHandler.CanAcceptData(dropInfo))
        {
            return false;
        }

        var data = ExtractData(dropInfo.Data).OfType<HocrNodeViewModel>().ToList();

        if (data.DistinctBy(n => n.NodeType).Count() > 1)
        {
            return false;
        }


        if (dropInfo.TargetItem is HocrNodeViewModel targetItem)
        {
            var isDroppingInto = dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter);

            if (isDroppingInto)
            {
                return targetItem.NodeType == HocrNodeTypeHelper.GetParentNodeType(data.First().NodeType);
            }

            if (dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.BeforeTargetItem) ||
                dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.AfterTargetItem))
            {
                return targetItem.Parent?.NodeType == HocrNodeTypeHelper.GetParentNodeType(data.First().NodeType);
            }
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
            new NodesMovedEventArgs(
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
