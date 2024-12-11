using System;
using SkiaSharp;

namespace HocrEditor.Shaders;

public interface IShader : IDisposable
{
    SKShader ToShader();
}
