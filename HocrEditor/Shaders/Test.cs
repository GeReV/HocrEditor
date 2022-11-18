using System.IO;
using SkiaSharp;

namespace HocrEditor.Shaders;

public class Test
{
//     public static void ShaderTest(SKCanvas canvas)
//     {
//         // input values
//         const float threshold = 1.05f;
//         const float exponent = 1.5f;
//
// // shader
//         const string src = @"
// in fragmentProcessor color_map;
//
// uniform float scale;
// uniform half exp;
// uniform float3 in_colors0;
//
// void main(float2 p, inout half4 color) {
//     half4 texColor = sample(color_map, p);
//     if (length(abs(in_colors0 - pow(texColor.rgb, half3(exp)))) < scale)
//         discard;
//     color = texColor;
// }";
//
//         using var effect = SKRuntimeEffect.Create(src, out var errorText);
//
// // input values
//         var inputs = new SKRuntimeEffectUniforms(effect)
//         {
//             { "scale", threshold },
//             { "exp", exponent },
//             { "in_colors0", new[] { 1f, 1f, 1f } },
//         };
//
//         // shader values
//         using var blueShirt = SKImage.FromEncodedData(Path.Combine(PathToImages, "blue-shirt.jpg"));
//         using var textureShader = blueShirt.ToShader();
//         var children = new SKRuntimeEffectChildren(effect)
//         {
//             { "color_map", textureShader },
//         };
//
//         // create actual shader
//         using var shader = effect.ToShader(isOpaque: true, inputs, children);
//
// // draw as normal
//         canvas.Clear(SKColors.Black);
//         using var paint = new SKPaint { Shader = shader };
//         canvas.DrawRect(SKRect.Create(400, 400), paint);
//     }
}
