using System;
using SkiaSharp;

namespace HocrEditor.Shaders;

public class RuntimeEffect : IShader
{
    private readonly bool isOpaque;
    private readonly SKRuntimeEffect effect;

    public RuntimeEffect(string source, bool isOpaque = false)
    {
        this.isOpaque = isOpaque;

        effect = SKRuntimeEffect.Create(source, out var errors);

        if (errors is not null)
        {
            throw new Exception(errors);
        }

        Uniforms = new SKRuntimeEffectUniforms(effect);
        Children = new SKRuntimeEffectChildren(effect);
    }

    public SKRuntimeEffectUniforms Uniforms { get; }

    public SKRuntimeEffectChildren Children { get; }

    public SKShader ToShader() => effect.ToShader(isOpaque, Uniforms, Children);

    public void Dispose()
    {
        effect.Dispose();

        GC.SuppressFinalize(this);
    }
}
