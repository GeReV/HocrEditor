using HocrEditor.Shaders;
using SkiaSharp;

namespace HocrEditor.ViewModels;

public sealed class GaussianBlurFilter : ImageFilterBase
{
    private readonly GaussianBlurEffect gaussianBlurEffect = new();

    public uint KernelSize
    {
        get => gaussianBlurEffect.KernelSize;
        set => gaussianBlurEffect.KernelSize = value;
    }

    public override string Name => "Blur";

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