using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HocrEditor.ViewModels.Filters;

namespace HocrEditor.ViewModels;

public static class AdjustmentFilterTypes
{
    private static readonly Lazy<IReadOnlyCollection<IAdjustmentFilterType>> AvailableAdjustmentFiltersLazy = new(
        () => Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ImageFilterBase)) && t.GetConstructor(Type.EmptyTypes) is not null)
            .Select(t => (IAdjustmentFilterType)Activator.CreateInstance(typeof(AdjustmentFilterType<>).MakeGenericType(t))!)
            .ToList()
    );

    public static IReadOnlyCollection<IAdjustmentFilterType> AvailableAdjustmentFilters =>
        AvailableAdjustmentFiltersLazy.Value;
}
