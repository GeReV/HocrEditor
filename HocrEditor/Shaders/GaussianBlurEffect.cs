using System;
using System.Linq;
using SkiaSharp;

namespace HocrEditor.Shaders;

public class GaussianBlurEffect : IShader
{
    private readonly SKImage image;

    private readonly RuntimeEffect horizontalPass;
    private readonly RuntimeEffect verticalPass;

    private const uint KERNEL_SIZE = 3;

    private static readonly string HorizontalPassSource = $@"
uniform shader image;
uniform vec2 imageResolution;
uniform float kernelSize;
uniform float[{KERNEL_SIZE}] kernel;

vec4 main(vec2 coord) {{
    vec4 sum = vec4(0.0);

    for (int i = 0; i < kernelSize; i++) {{
        float offset = i + (kernelSize - 1);
        sum += sample(image, vec2(coord.x + offset, coord.y)) * kernel[i];
    }}

    return sum;
}}
";

    private static readonly string VerticalPassSource = $@"
uniform shader image;
uniform vec2 imageResolution;
uniform float kernelSize;
uniform float[{KERNEL_SIZE}] kernel;

vec4 main(vec2 coord) {{
    vec4 sum = vec4(0.0);

    for (int i = 0; i < kernelSize; i++) {{
        float offset = i + (kernelSize - 1);
        sum += sample(image, vec2(coord.x, coord.y + offset)) * kernel[i];
    }}

    return sum;
}}
";

    // https://docs.opencv.org/3.3.1/d4/d86/group__imgproc__filter.html#gac05a120c1ae92a6060dd0db190a61afa
    public static float[] GetGaussianKernel(uint size)
    {
        if ((size & 1) == 0)
        {
            throw new Exception("Expected size to be an odd number");
        }

        var sigma = 0.3f * ((size - 1) * 0.5f - 1) + 0.8f;

        var coefficients = new float[size];

        for (var i = 0; i < size; i++)
        {
            var k = i - (size - 1) * 0.5f;

            coefficients[i] = (float)Math.Exp(-k * k / (2 * sigma * sigma));
        }

        var scale = 1.0f / coefficients.Sum();

        for (var i = 0; i < coefficients.Length; i++)
        {
            coefficients[i] *= scale;
        }

        return coefficients;
    }

    public GaussianBlurEffect(SKImage image)
    {
        this.image = image;

        var coefficients = GetGaussianKernel(KERNEL_SIZE);

        horizontalPass = new RuntimeEffect(HorizontalPassSource);
        verticalPass = new RuntimeEffect(VerticalPassSource);

        horizontalPass.Children["image"] = image.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);

        verticalPass.Uniforms["imageResolution"] =
            horizontalPass.Uniforms["imageResolution"] = new float[] { image.Width, image.Height };
        verticalPass.Uniforms["kernel"] = horizontalPass.Uniforms["kernel"] = coefficients;
        verticalPass.Uniforms["kernelSize"] = horizontalPass.Uniforms["kernelSize"] = coefficients.Length;

        verticalPass.Children["image"] = horizontalPass.ToShader();
    }

    public SKShader ToShader() => verticalPass.ToShader();

    public void Dispose()
    {
        verticalPass.Dispose();
        horizontalPass.Dispose();

        GC.SuppressFinalize(this);
    }
}
