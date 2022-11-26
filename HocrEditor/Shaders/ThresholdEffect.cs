using SkiaSharp;

namespace HocrEditor.Shaders;

public class ThresholdEffect : RuntimeEffect
{
    private SKShader image = SKShader.CreateEmpty();
    private float threshold;

    private const string SOURCE = @"
uniform shader image;
uniform float threshold;

vec4 main(vec2 coord) {
    float binary = sample(image, coord).r >= threshold ? 1.0 : 0.0;

    return vec4(binary).rgb1;
}";

    public ThresholdEffect() : base(SOURCE) {}

    public ThresholdEffect(SKShader image, float threshold = 0.5f) : this()
    {
        Image = image;
        Threshold = threshold;
    }

    public SKShader Image
    {
        get => image;
        set
        {
            image = value;
            Children["image"] = value;
        }
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
