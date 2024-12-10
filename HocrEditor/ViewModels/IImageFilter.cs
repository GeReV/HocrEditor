using System;
using System.ComponentModel;
using SkiaSharp;

namespace HocrEditor.ViewModels;

public interface IImageFilter : INotifyPropertyChanged, IDisposable
{
    SKShader Compose(SKShader source, SKImageInfo imageInfo);

    bool ShouldUpdateImageOnPropertyChange(string propertyName);

    bool IsEnabled { get; set; }

    string Name { get; }
}
