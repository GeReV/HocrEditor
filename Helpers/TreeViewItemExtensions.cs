using System.Windows.Controls;

namespace HocrEditor.Helpers;

public static class TreeViewItemExtensions
{
    public static TreeViewItem? FindChildFromItem(this TreeViewItem item, object obj) =>
        FindChildFromItem(item.ItemContainerGenerator, obj);

    public static TreeViewItem? FindChildFromItem(this TreeView treeView, object obj) =>
        FindChildFromItem(treeView.ItemContainerGenerator, obj);

    private static TreeViewItem? FindChildFromItem(ItemContainerGenerator generator, object obj)
    {
        var container = generator.ContainerFromItem(obj);

        if (container is TreeViewItem treeViewItem)
        {
            return treeViewItem;
        }

        for (var i = 0; i < generator.Items.Count; i++)
        {
            var childContainer = generator.ContainerFromIndex(i);

            if (childContainer is not TreeViewItem childTreeViewItem)
            {
                continue;
            }

            var target = FindChildFromItem(childTreeViewItem.ItemContainerGenerator, obj);
            if (target != null)
            {
                return target;
            }
        }

        return null;
    }
}
