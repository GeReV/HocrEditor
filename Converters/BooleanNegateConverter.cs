using System;
using System.Globalization;
using System.Windows.Data;

namespace HocrEditor.Converters;

public class BooleanNegateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // ReSharper disable once MergeIntoPattern
        return value is bool b && !b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        // ReSharper disable once MergeIntoPattern
        return value is bool b && !b;
    }
}
