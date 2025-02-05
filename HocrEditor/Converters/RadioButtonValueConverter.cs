using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace HocrEditor.Converters;

public class RadioButtonValueConverter(object optionValue) : MarkupExtension, IValueConverter
{
    private object OptionValue { get; } = optionValue;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value != null && value.Equals(OptionValue);

    public object ConvertBack(object? isChecked, Type targetType, object? parameter, CultureInfo culture)
        => (bool)(isChecked ?? false) // Is this the checked RadioButton? If so...
            ? OptionValue // Send 'OptionValue' back to update the associated binding. Otherwise...
            : Binding.DoNothing; // Return Binding.DoNothing, telling the binding 'ignore this change'

    public override object ProvideValue(IServiceProvider serviceProvider)
        => this;
}
