using System;
using System.Globalization;
using System.Windows.Data;

namespace HocrEditor.Converters;

public class StringToBooleanConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string s)
        {
            return !string.IsNullOrEmpty(s);
        }

        return value is not null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
