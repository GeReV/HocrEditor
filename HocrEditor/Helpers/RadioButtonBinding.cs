using System.Windows.Data;
using HocrEditor.Converters;

namespace HocrEditor.Helpers;

public class RadioButtonBinding : Binding
{
    public RadioButtonBinding(string path, object optionValue)
        : base(path)
    {
        Converter = new RadioButtonValueConverter(optionValue);
    }
}
