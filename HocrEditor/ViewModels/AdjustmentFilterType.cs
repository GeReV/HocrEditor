using HocrEditor.ViewModels.Filters;

namespace HocrEditor.ViewModels;

public class AdjustmentFilterType<T> : IAdjustmentFilterType where T : ImageFilterBase, IImageFilter, new()
{
    public string Name => T.Name;

    ImageFilterBase IAdjustmentFilterType.Create() => new T();
}
