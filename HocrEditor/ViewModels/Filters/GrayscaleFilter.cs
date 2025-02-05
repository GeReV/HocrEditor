using System;
using System.Drawing.Imaging;
using SkiaSharp;

namespace HocrEditor.ViewModels.Filters;

public sealed class GrayscaleFilter : ImageFilterBase, IImageFilter
{
    private static readonly SKColorFilter LumaColorFilter = SKColorFilter.CreateColorMatrix(
        [
            0.299f, 0.587f, 0.114f, 0.0f, 0.0f,
            0.299f, 0.587f, 0.114f, 0.0f, 0.0f,
            0.299f, 0.587f, 0.114f, 0.0f, 0.0f,
            0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
        ]
    );

    private static readonly SKColorFilter[] SingleChannelColorFilters =
    [
        SKColorFilter.CreateColorMatrix(
            [
                1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                1.0f, 0.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
            ]
        ),
        SKColorFilter.CreateColorMatrix(
            [
                0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
            ]
        ),
        SKColorFilter.CreateColorMatrix(
            [
                0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 1.0f, 0.0f, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
            ]
        ),
    ];

    public static string Name => "Grayscale";

    public FilterKind Kind { get; set; } = FilterKind.Luma;

    public override SKShader Compose(SKShader source, SKImageInfo imageInfo) =>
        SKShader.CreateColorFilter(
            source,
            Kind switch
            {
                FilterKind.Luma => LumaColorFilter,
                FilterKind.RedChannel => SingleChannelColorFilters[0],
                FilterKind.GreenChannel => SingleChannelColorFilters[1],
                FilterKind.BlueChannel => SingleChannelColorFilters[2],
                _ => throw new ArgumentOutOfRangeException(nameof(Kind)),
            }
        );

    public override bool ShouldUpdateImageOnPropertyChange(string propertyName) => true;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            LumaColorFilter.Dispose();

            foreach (var filter in SingleChannelColorFilters)
            {
                filter.Dispose();
            }
        }
    }

    public enum FilterKind
    {
        Luma,
        RedChannel,
        GreenChannel,
        BlueChannel,
    }
}
