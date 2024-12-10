using System;
using System.Linq;
using SkiaSharp;

namespace HocrEditor.Shaders;

public class GaussianBlurEffect(uint kernelSize = 3) : IShader
{
    private const uint MAX_KERNEL_SIZE = 63;

    private uint kernelSize = kernelSize;

    private static string HorizontalPassSource(uint kernelSize) => $$"""
                                                                     uniform shader child;
                                                                     uniform float[{{kernelSize}}] kernel;

                                                                     vec4 main(vec2 coord) {
                                                                         vec4 sum = vec4(0.0);

                                                                         for (int i = 0; i < {{kernelSize}}; i++) {
                                                                             float offset = float(i) - float({{kernelSize - 1}}) * 0.5;
                                                                             sum += child.eval(vec2(coord.x + offset, coord.y)) * kernel[i];
                                                                         }

                                                                         return sum;
                                                                     }
                                                                     """;

    private static string VerticalPassSource(uint kernelSize) => $$"""
                                                                   uniform shader child;
                                                                   uniform float[{{kernelSize}}] kernel;

                                                                   vec4 main(vec2 coord) {
                                                                       vec4 sum = vec4(0.0);

                                                                       for (int i = 0; i < {{kernelSize}}; i++) {
                                                                           float offset = float(i) - float({{kernelSize - 1}}) * 0.5;
                                                                           sum += child.eval(vec2(coord.x, coord.y + offset)) * kernel[i];
                                                                       }

                                                                       return sum;
                                                                   }
                                                                   """;

    private RuntimeEffect horizontalPass = new(HorizontalPassSource(kernelSize));
    private RuntimeEffect verticalPass = new(VerticalPassSource(kernelSize));

    public SKShader Image { get; set; } = SKShader.CreateEmpty();

    public uint KernelSize
    {
        get => kernelSize;
        set
        {
            var newSize = Math.Clamp(value, 1, MAX_KERNEL_SIZE);
            if (kernelSize == newSize)
            {
                return;
            }

            kernelSize = newSize;

            RebuildShaders();
        }
    }

    // https://docs.opencv.org/3.3.1/d4/d86/group__imgproc__filter.html#gac05a120c1ae92a6060dd0db190a61afa
    private static float[] GetGaussianKernel(uint size)
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

    private void RebuildShaders()
    {
        horizontalPass.Dispose();
        verticalPass.Dispose();

        horizontalPass = new RuntimeEffect(HorizontalPassSource(kernelSize));
        verticalPass = new RuntimeEffect(VerticalPassSource(kernelSize));
    }

    public SKShader ToShader()
    {
        verticalPass.Uniforms["kernel"] = horizontalPass.Uniforms["kernel"] = GetGaussianKernel(kernelSize);

        horizontalPass.Children["child"] = Image;
        verticalPass.Children["child"] = horizontalPass.ToShader();

        return verticalPass.ToShader();
    }

    public void Dispose()
    {
        verticalPass.Dispose();
        horizontalPass.Dispose();

        Image.Dispose();

        GC.SuppressFinalize(this);
    }
}
