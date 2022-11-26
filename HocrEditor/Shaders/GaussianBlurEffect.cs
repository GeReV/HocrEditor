using System;
using System.Linq;
using SkiaSharp;

namespace HocrEditor.Shaders;

public class GaussianBlurEffect : IShader
{
    private SKShader image = SKShader.CreateEmpty();
    private uint kernelSize = 3;

    private readonly RuntimeEffect horizontalPass = new(HorizontalPassSource);
    private readonly RuntimeEffect verticalPass = new(VerticalPassSource);

    private const uint MAX_KERNEL_SIZE = 63;

    private static readonly string HorizontalPassSource = $@"
uniform shader image;
uniform float kernelSize;
uniform float[{MAX_KERNEL_SIZE}] kernel;

vec4 main(vec2 coord) {{
    vec4 sum = vec4(0.0);

    for (int i = 0; i < kernelSize; i++) {{
        float offset = i - (kernelSize - 1) * 0.5;
        sum += sample(image, vec2(coord.x + offset, coord.y)) * kernel[i];
    }}

    return sum;
}}
";

    private static readonly string VerticalPassSource = $@"
uniform shader image;
uniform float kernelSize;
uniform float[{MAX_KERNEL_SIZE}] kernel;

vec4 main(vec2 coord) {{
    vec4 sum = vec4(0.0);

    for (int i = 0; i < kernelSize; i++) {{
        float offset = i - (kernelSize - 1) * 0.5;
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

    public GaussianBlurEffect()
    {
    }

    public GaussianBlurEffect(SKShader image, uint kernelSize = 3)
    {
        Image = image;
        KernelSize = kernelSize;
    }

    public SKShader Image
    {
        get => image;
        set
        {
            image = value;
            horizontalPass.Children["image"] = value;
        }
    }

    public uint KernelSize
    {
        get => kernelSize;
        set
        {
            kernelSize = Math.Clamp(value, 1, MAX_KERNEL_SIZE);

            var kernel = new float[MAX_KERNEL_SIZE];
            var coefficients = GetGaussianKernel(kernelSize);

            Array.Copy(coefficients, kernel, coefficients.Length);

            verticalPass.Uniforms["kernel"] = horizontalPass.Uniforms["kernel"] = kernel;
            verticalPass.Uniforms["kernelSize"] = horizontalPass.Uniforms["kernelSize"] = kernelSize;
        }
    }

    public SKShader ToShader()
    {
        verticalPass.Children["image"] = horizontalPass.ToShader();

        return verticalPass.ToShader();
    }

    public void Dispose()
    {
        verticalPass.Dispose();
        horizontalPass.Dispose();

        GC.SuppressFinalize(this);
    }
}
