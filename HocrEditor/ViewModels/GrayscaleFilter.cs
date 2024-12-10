using HocrEditor.Shaders;
using SkiaSharp;

namespace HocrEditor.ViewModels;

public sealed class GrayscaleFilter : ImageFilterBase
{
    private static readonly float[] GrayscaleMatrix =
    [
        0.299f, 0.587f, 0.114f, 0.0f, 0.0f,
        0.299f, 0.587f, 0.114f, 0.0f, 0.0f,
        0.299f, 0.587f, 0.114f, 0.0f, 0.0f,
        0.0f, 0.0f, 0.0f, 1.0f, 0.0f,
    ];

    private static readonly SKColorFilter GrayscaleImageFilter = SKColorFilter.CreateColorMatrix(GrayscaleMatrix);

    public override string Name => "Grayscale";

    public override SKShader Compose(SKShader source, SKImageInfo imageInfo) =>
        SKShader.CreateColorFilter(source, GrayscaleImageFilter);

    public override bool ShouldUpdateImageOnPropertyChange(string propertyName) => true;

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            GrayscaleImageFilter.Dispose();
        }
    }
}