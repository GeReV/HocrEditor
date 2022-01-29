using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HocrEditor.Helpers;

public static class StringExtensions
{
    // https://stackoverflow.com/a/15111719/242826
    private static IEnumerable<string> GraphemeClusters(this string s) {
        var enumerator = StringInfo.GetTextElementEnumerator(s);

        while (enumerator.MoveNext())
        {
            yield return (string)enumerator.Current;
        }
    }

    public static string Reverse(this string s) {
        return string.Join("", s.GraphemeClusters().Reverse().ToArray());
    }
}
