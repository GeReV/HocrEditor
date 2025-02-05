using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace HocrEditor.Converters;

public class NullableBitmapSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var uri = value switch
        {
            string s => new Uri(s, UriKind.Relative),
            Uri u => u,
            _ => null,
        };

        if (uri == null)
        {
            return null;
        }

        return new BitmapImage(uri);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value switch
        {
            BitmapImage bitmapImage => bitmapImage.UriSource.ToString(),
            _ => null,
        };
}
