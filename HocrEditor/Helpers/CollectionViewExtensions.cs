using System.ComponentModel;
using System.Linq;

namespace HocrEditor.Helpers;

public static class CollectionViewExtensions
{
    public static int Count(this ICollectionView collectionView) => collectionView.Cast<object>().Count();

    public static bool IsCurrentFirst(this ICollectionView collectionView) =>
        collectionView.CurrentItem != null && collectionView.CurrentPosition == 0;

    public static bool IsCurrentLast(this ICollectionView collectionView) =>
        collectionView.CurrentItem != null && collectionView.CurrentPosition == collectionView.Count() - 1;
}
