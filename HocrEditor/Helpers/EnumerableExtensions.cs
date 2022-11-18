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

    public static IDictionary<TRet, uint> CountBy<TSource, TRet>(this IEnumerable<TSource> enumerable, Func<TSource, TRet> selector) where TRet : notnull
    {
        var dictionary = new Dictionary<TRet, uint>();

        foreach (var item in enumerable)
        {
            var key = selector(item);
            if (dictionary.ContainsKey(key))
            {
                dictionary[key] += 1;
            }
            else
            {
                dictionary[key] = 1;
            }
        }

        if (typeof(TRet).IsEnum)
        {
            var values = Enum.GetValues(typeof(TRet));

            foreach (TRet value in values)
            {
                if (!dictionary.ContainsKey(value))
                {
                    dictionary[value] = 0;
                }
            }
        }

        return dictionary;
    }

    public static IEnumerable<float> CumulativeSum(this IList<float> source)
    {
        var result = new float[source.Count];

        result[0] = source[0];

        for (var i = 1; i < source.Count; i++)
        {
            result[i] = result[i - 1] + source[i];
        }

        return result;
    }

    public static IEnumerable<int> CumulativeSum(this IList<int> source)
    {
        var result = new int[source.Count];

        result[0] = source[0];

        for (var i = 1; i < source.Count; i++)
        {
            result[i] = result[i - 1] + source[i];
        }

        return result;
    }
}
