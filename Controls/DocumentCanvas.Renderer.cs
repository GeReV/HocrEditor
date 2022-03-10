using System;
using System.Linq;
using HocrEditor.Helpers;
using HocrEditor.Models;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace HocrEditor.Controls;

public partial class DocumentCanvas
{
    private void RenderNodes(SKCanvas canvas)
    {
        if (rootId < 0)
        {
            return;
        }

        using var shaper = new SKShaper(SKTypeface.Default);
        using var paint = new SKPaint(new SKFont(SKTypeface.Default))
        {
            StrokeWidth = 1
        };

        const float counterFontSize = 9.0f;
        var counterTextSize = counterFontSize / 72.0f * ((HocrPage)elements[rootId].Item1.HocrNode).Dpi.Item2;

        void Recurse(int key, int index)
        {
            var (node, element) = elements[key];

            var bounds = transformation.MapRect(element.Bounds);

            var shouldRenderNode = nodeVisibilityDictionary[node.NodeType];

            if (element.Background != null)
            {
                if (shouldRenderNode)
                {
                    canvas.DrawBitmap(element.Background, bounds);
                }
                else
                {
                    paint.IsStroke = false;
                    paint.Color = SKColors.White;

                    canvas.DrawRect(bounds, paint);
                }

                paint.Color = SKColors.Gray;
                paint.IsStroke = true;
                paint.StrokeWidth = 1;

                canvas.DrawRect(bounds, paint);
            }
            else
            {
                if (shouldRenderNode)
                {
                    var color = GetNodeColor(node);

                    var scale = transformation.ScaleY;

                    if (node.NodeType == HocrNodeType.Word && IsShowText)
                    {
                        paint.IsStroke = false;
                        paint.Color = SKColors.White.WithAlpha(128);

                        canvas.DrawRect(bounds, paint);

                        var fontSize = ((HocrLine)elements[node.ParentId].Item1.HocrNode).FontSize;

                        paint.TextSize = fontSize * scale * 0.75f;

                        paint.Color = SKColors.Black;
                        paint.IsStroke = false;

                        var textBounds = SKRect.Empty;

                        paint.MeasureText(node.InnerText, ref textBounds);

                        canvas.DrawShapedText(
                            shaper,
                            node.InnerText,
                            bounds.MidX - textBounds.MidX,
                            bounds.MidY - textBounds.MidY,
                            paint
                        );
                    }
                    else
                    {
                        paint.IsStroke = false;
                        paint.Color = node.IsSelected ? SKColors.Red.WithAlpha(16) : color.WithAlpha(16);

                        canvas.DrawRect(bounds, paint);
                    }

                    if (IsShowNumbering && index >= 0)
                    {
                        paint.TextSize = counterTextSize * scale;

                        var rectBounds = SKRect.Empty;
                        paint.MeasureText("99", ref rectBounds);

                        paint.IsStroke = false;
                        paint.Color = color;

                        rectBounds.Bottom += rectBounds.Height * 0.2f;
                        rectBounds.Location = bounds.Location;

                        canvas.DrawRect(rectBounds, paint);

                        paint.Color = SKColors.Black;

                        var text = index.ToString();

                        var textBounds = SKRect.Empty;
                        paint.MeasureText(text, ref textBounds);

                        textBounds.Offset(rectBounds.MidX - textBounds.MidX, rectBounds.MidY - textBounds.MidY);

                        canvas.DrawText(text, textBounds.Left, textBounds.Bottom, paint);
                    }

                    paint.Color = node.IsSelected ? SKColors.Red : color;
                    paint.IsStroke = true;

                    canvas.DrawRect(bounds, paint);
                }
            }

            var counter = node.Children.Count > 1 ? 0 : -1;
            foreach (var childKey in node.Children.Select(c => c.Id))
            {
                Recurse(childKey, counter >= 0 ? ++counter : counter);
            }
        }

        Recurse(rootId, -1);

        canvasSelection.Render(
            canvas,
            transformation,
            ActiveTool switch
            {
                DocumentCanvasTool.None => NodeSelectionColor,
                DocumentCanvasTool.SelectionTool => SKColor.Empty,
                DocumentCanvasTool.WordSplitTool => HighlightColor,
                _ => throw new ArgumentOutOfRangeException()
            }
        );

        RenderNodeSelection(canvas);

        RenderWordSplitter(canvas);
    }

    private void RenderNodeSelection(SKCanvas canvas)
    {
        if (nodeSelection.IsEmpty)
        {
            return;
        }

        var bbox = transformation.MapRect(nodeSelection);

        var paint = new SKPaint
        {
            IsStroke = false,
            Color = NodeSelectorColor.WithAlpha(64),
        };

        canvas.DrawRect(bbox, paint);

        paint.IsStroke = true;
        paint.Color = NodeSelectorColor;

        canvas.DrawRect(bbox, paint);
    }

    private void RenderWordSplitter(SKCanvas canvas)
    {
        if (wordSplitterPosition.IsEmpty)
        {
            return;
        }

        var selectedElement = elements[selectedElements.First()].Item2;

        var bounds = transformation.MapRect(selectedElement.Bounds);
        var point = transformation.MapPoint(wordSplitterPosition);

        canvas.DrawDashedLine(point.X, bounds.Top, point.X, bounds.Bottom, HighlightColor);
    }
}
