using SkiaSharp;

namespace HocrEditor.Shaders;

public class ThresholdEffect : RuntimeEffect
{
    private float threshold;

    private const string SOURCE = @"
uniform shader image;
uniform float threshold;

vec4 main(vec2 coord) {
    const vec3 luma = vec3(0.299, 0.587, 0.114);

    float binary = dot(sample(image, coord).rgb, luma) >= threshold ? 1.0 : 0.0;

    return vec4(binary).rgb1;
}";

    public ThresholdEffect(SKShader image, float threshold = 0.5f) : base(SOURCE)
    {
        Children["image"] = image;

        Threshold = threshold;
    }

    public float Threshold
    {
        get => threshold;
        set
        {
            threshold = value;
            Uniforms["threshold"] = value;
        }
    }
}
