using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HocrEditor.Helpers
{
    public static class RecursiveSelectHelper
    {
        // https://stackoverflow.com/a/30441479/242826
        public static IEnumerable<TSource> RecursiveSelect<TSource>(
            this IEnumerable<TSource> source, Func<TSource, IEnumerable<TSource>> childSelector)
        {
            var stack = new Stack<IEnumerator<TSource>>();
            var enumerator = source.GetEnumerator();

            try
            {
                while (true)
                {
                    if (enumerator.MoveNext())
                    {
                        var element = enumerator.Current;
                        yield return element;

                        stack.Push(enumerator);
                        enumerator = childSelector(element).GetEnumerator();
                    }
                    else if (stack.Count > 0)
                    {
                        enumerator.Dispose();
                        enumerator = stack.Pop();
                    }
                    else
                    {
                        yield break;
                    }
                }
            }
            finally
            {
                enumerator.Dispose();

                while (stack.Count > 0) // Clean up in case of an exception.
                {
                    enumerator = stack.Pop();
                    enumerator.Dispose();
                }
            }
        }

        [StructLayout(LayoutKind.Auto)]
        public readonly struct RecursionItem<T>
        {
            public T Item { get; }
            public int LevelIndex { get; }
            public int OverallIndex { get; }

            public RecursionItem(T item, int levelIndex, int overallIndex)
            {
                Item = item;
                LevelIndex = levelIndex;
                OverallIndex = overallIndex;
            }
        }

        public static IEnumerable<RecursionItem<TSource>> IndexedRecursiveSelect<TSource>(
            this IEnumerable<TSource> source, Func<TSource, IEnumerable<TSource>> childSelector)
        {
            var stack = new Stack<IEnumerator<TSource>>();
            var indexStack = new Stack<int>();
            var enumerator = source.GetEnumerator();

            var overallIndex = 0;
            var levelIndex = 0;

            try
            {
                while (true)
                {
                    if (enumerator.MoveNext())
                    {
                        var element = enumerator.Current;
                        yield return new RecursionItem<TSource>(element, levelIndex++, overallIndex++);

                        stack.Push(enumerator);
                        indexStack.Push(levelIndex);

                        enumerator = childSelector(element).GetEnumerator();

                        levelIndex = 0;
                    }
                    else if (stack.Count > 0)
                    {
                        enumerator.Dispose();
                        enumerator = stack.Pop();

                        levelIndex = indexStack.Pop();
                    }
                    else
                    {
                        yield break;
                    }
                }
            }
            finally
            {
                enumerator.Dispose();

                while (stack.Count > 0) // Clean up in case of an exception.
                {
                    enumerator = stack.Pop();
                    enumerator.Dispose();
                }
            }
        }
    }
}
