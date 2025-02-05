using HocrEditor.ViewModels.Filters;

namespace HocrEditor.ViewModels;

public interface IAdjustmentFilterType
{
    string Name { get; }

    ImageFilterBase Create();
}
