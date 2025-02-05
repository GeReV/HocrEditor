using HocrEditor.Shaders;
using SkiaSharp;

namespace HocrEditor.ViewModels.Filters;

public sealed class GaussianBlurFilter : ImageFilterBase, IImageFilter
{
    private readonly GaussianBlurEffect gaussianBlurEffect = new();

    public uint KernelSize
    {
        get => gaussianBlurEffect.KernelSize;
        set => gaussianBlurEffect.KernelSize = value;
    }

    public static string Name => "Gaussian Blur";

    public override SKShader Compose(SKShader source, SKImageInfo imageInfo)
    {
        gaussianBlurEffect.Image = source;

        return gaussianBlurEffect.ToShader();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            gaussianBlurEffect.Dispose();
        }
    }

    public override bool ShouldUpdateImageOnPropertyChange(string propertyName) => true;
}
