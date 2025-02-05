using SkiaSharp;

namespace HocrEditor.Shaders;

public class GrayscaleEffect() : RuntimeEffect(SOURCE)
{
    private SKShader image = SKShader.CreateEmpty();

    private const string SOURCE = """

                                  uniform shader child;

                                  vec4 main(vec2 coord) {
                                      const vec3 luma = vec3(0.299, 0.587, 0.114);

                                      return vec3(dot(child.eval(coord).rgb, luma)).rgb1;
                                  }
                                  """;

    public GrayscaleEffect(SKShader image) : this()
    {
        Image = image;
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
}
