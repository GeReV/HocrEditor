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

            if (dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.BeforeTargetItem) || dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.AfterTargetItem))
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

        var isTreeViewItem = dropInfo.InsertPosition.HasFlag(RelativeInsertPosition.TargetItemCenter) && dropInfo.VisualTargetItem is TreeViewItem;
        dropInfo.DropTargetAdorner = isTreeViewItem ? DropTargetAdorners.Highlight : DropTargetAdorners.Insert;
    }

    public override void Drop(IDropInfo? dropInfo)
    {
        if (dropInfo?.DragInfo == null)
        {
            return;
        }

        var insertIndex = GetInsertIndex(dropInfo);
        var destinationList = dropInfo.TargetCollection.TryGetList();
        var data = ExtractData(dropInfo.Data).OfType<object>().ToList();
        var isSameCollection = false;

        var copyData = ShouldCopyData(dropInfo);
        if (!copyData)
        {
            var sourceList = dropInfo.DragInfo.SourceCollection.TryGetList();
            if (sourceList != null)
            {
                isSameCollection = sourceList.IsSameObservableCollection(destinationList);
                if (!isSameCollection)
                {
                    foreach (var o in data)
                    {
                        var index = sourceList.IndexOf(o);
                        if (index == -1)
                        {
                            continue;
                        }

                        sourceList.RemoveAt(index);

                        // If source is destination too fix the insertion index
                        if (destinationList != null && ReferenceEquals(sourceList, destinationList) &&
                            index < insertIndex)
                        {
                            --insertIndex;
                        }
                    }
                }
            }
        }

        if (destinationList == null)
        {
            return;
        }

        var objects2Insert = new List<object>();

        // check for cloning
        var cloneData = dropInfo.Effects.HasFlag(DragDropEffects.Copy) ||
                        dropInfo.Effects.HasFlag(DragDropEffects.Link);

        foreach (var o in data)
        {
            var obj2Insert = o;
            if (cloneData)
            {
                if (o is ICloneable cloneable)
                {
                    obj2Insert = cloneable.Clone();
                }
            }

            objects2Insert.Add(obj2Insert);

            if (!cloneData && isSameCollection)
            {
                var index = destinationList.IndexOf(o);
                if (index == -1)
                {
                    continue;
                }

                if (insertIndex > index)
                {
                    insertIndex--;
                }

                Move(destinationList, index, insertIndex++);
            }
            else
            {
                destinationList.Insert(insertIndex++, obj2Insert);
            }
        }

        SelectDroppedItems(dropInfo, objects2Insert);
    }
}
