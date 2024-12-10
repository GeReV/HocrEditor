using System;
using System.Diagnostics;
using HocrEditor.Core;
using SkiaSharp;

namespace HocrEditor.Shaders;

public class RuntimeEffect : IShader
{
    private readonly SKRuntimeEffect effect;

    public RuntimeEffect(string source)
    {
        effect = SKRuntimeEffect.CreateShader(source, out var errors);

        if (errors is not null)
        {
            throw new Exception(errors);
        }

        Uniforms = new SKRuntimeEffectUniforms(effect);
        Children = new SKRuntimeEffectChildren(effect);
    }

    public SKRuntimeEffectUniforms Uniforms { get; }

    public SKRuntimeEffectChildren Children { get; }

    public SKShader ToShader() => effect.ToShader(Uniforms, Children);

    public void Dispose()
    {
        effect.Dispose();

        GC.SuppressFinalize(this);
    }
}
