using System;
using System.ComponentModel;
using SkiaSharp;

namespace HocrEditor.ViewModels.Filters;

public interface IImageFilter : INotifyPropertyChanged, IDisposable
{
    static virtual string Name { get; }

    SKShader Compose(SKShader source, SKImageInfo imageInfo);

    bool ShouldUpdateImageOnPropertyChange(string propertyName);

    bool IsEnabled { get; set; }

}
