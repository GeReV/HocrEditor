using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace HocrEditor.Converters;

public class ContentEmptyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Value may be one or more nested empty ContentPresenters inside the content, which should be considered empty.
        // If it's not, just return whether the content itself is empty.
        if (value is not ContentPresenter presenter)
        {
            return value is null;
        }

        // If the nested case above is true, drill down to the last ContentPresenter, and check if the last one is empty.
        while (presenter.Content is ContentPresenter content)
        {
            presenter = content;
        }

        return presenter.Content is null;

    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
