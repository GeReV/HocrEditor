// Adapted from: https://gist.github.com/awatertrevi/68924981bdea1800f5af162e4eb2b1f5
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Data;

namespace HocrEditor.Converters;

public class ValueConverterGroup : List<IValueConverter>, IValueConverter
{
    private string[]? parameters;
    private bool shouldReverse;

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        ExtractParameters(parameter);

        if (shouldReverse)
        {
            Reverse();
            shouldReverse = false;
        }

        return this.Aggregate(value, (current, converter) => converter.Convert(current, targetType, GetParameter(converter), culture));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        ExtractParameters(parameter);

        Reverse();
        shouldReverse = true;

        return this.Aggregate(value, (current, converter) => converter.ConvertBack(current, targetType, GetParameter(converter), culture));
    }

    private void ExtractParameters(object? parameter)
    {
        if (parameter != null)
        {
            parameters = Regex.Split(parameter.ToString() ?? string.Empty, @"(?<!\\),");
        }
    }

    private string? GetParameter(IValueConverter converter)
    {
        if (parameters == null)
            return null;

        var index = IndexOf(converter);
        string? parameter;

        try
        {
            parameter = parameters[index];
        }

        catch (IndexOutOfRangeException)
        {
            parameter = null;
        }

        if (parameter != null)
            parameter = Regex.Unescape(parameter);

        return parameter;
    }
}
