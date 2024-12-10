using SkiaSharp;

namespace HocrEditor.ViewModels;

public abstract class ImageFilterBase : ViewModelBase, IImageFilter
{
    // Filter initially requires update.
    private bool requiresUpdate = true;

    public abstract string Name { get; }

    public bool IsEnabled { get; set; } = true;

    public void MarkForUpdate() => requiresUpdate = true;

    public void Update(SKShader source, SKImageInfo imageInfo)
    {
        if (requiresUpdate)
        {
            PerformUpdate(source, imageInfo);

            requiresUpdate = false;
        }
    }

    protected virtual void PerformUpdate(SKShader source, SKImageInfo imageInfo)
    {
    }

    public abstract SKShader Compose(SKShader source, SKImageInfo imageInfo);

    public abstract bool ShouldUpdateImageOnPropertyChange(string propertyName);
}