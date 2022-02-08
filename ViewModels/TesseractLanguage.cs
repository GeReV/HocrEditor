using System.Globalization;

namespace HocrEditor.ViewModels;

public class TesseractLanguage : ViewModelBase
{
    public TesseractLanguage(string language, bool isSelected = false)
    {
        Language = language;
        IsSelected = isSelected;
    }

    public string DisplayText => CultureInfo.CurrentUICulture.TextInfo.ToTitleCase(Language);

    public string Language { get; }

    public bool IsSelected { get; set; }

    public override void Dispose()
    {
        // No-op.
    }
}
