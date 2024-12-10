using SkiaSharp;

namespace HocrEditor.Shaders;

public class ThresholdEffect() : RuntimeEffect(SOURCE)
{
    private SKShader image = SKShader.CreateEmpty();
    private float threshold;

    private const string SOURCE = """
                                  uniform shader child;
                                  uniform float threshold;

                                  vec4 main(vec2 coord) {
                                      float binary = child.eval(coord).r >= threshold ? 1.0 : 0.0;

                                      return vec4(binary).rgb1;
                                  }
                                  """;

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
            Children["child"] = value;
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
