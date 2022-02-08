using System;
using System.Collections.Generic;

namespace HocrEditor.Helpers;

public static class EnumerableExtensions
{
    public static (IEnumerable<T>, IEnumerable<T>) Partition<T>(this IEnumerable<T> enumerable, Predicate<T> predicate)
    {
        var a = new List<T>();
        var b = new List<T>();

        foreach (var item in enumerable)
        {
            if (predicate(item))
            {
                a.Add(item);
            }
            else
            {
                b.Add(item);
            }
        }

        return (a, b);
    }
}
