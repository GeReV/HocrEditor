using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using Icu;
using SkiaSharp;
using SkiaSharp.HarfBuzz;

namespace HocrEditor.Controls;

public partial class DocumentCanvas
{
    private BiDi bidi = new();

    private void RenderCanvas(SKCanvas canvas)
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

        RenderBackground(canvas, paint);

        RenderNodes(canvas, paint, shaper);

        if (IsShowNumbering)
        {
            RenderNumbering(canvas, paint);
        }

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

    private void RenderNumbering(SKCanvas canvas, SKPaint paint)
    {
        const float counterFontSize = 9.0f;
        var counterTextSize = counterFontSize / 72.0f * ((HocrPage)elements[rootId].Item1.HocrNode).Dpi.Item2;

        var stack = new Stack<int>();

        stack.Push(rootId);

        foreach (var recursionItem in RecurseNodes(rootId))
        {
            var (node, element) = recursionItem.Item;

            var shouldRenderNode = nodeVisibilityDictionary[node.NodeType];

            if (!shouldRenderNode)
            {
                continue;
            }

            var bounds = transformation.MapRect(element.Bounds);

            var color = GetNodeColor(node);

            var scale = transformation.ScaleY;

            paint.TextSize = counterTextSize * scale;

            var rectBounds = SKRect.Empty;
            paint.MeasureText("99", ref rectBounds);

            paint.IsStroke = false;
            paint.Color = color;

            rectBounds.Bottom += rectBounds.Height * 0.2f;
            rectBounds.Location = bounds.Location;

            canvas.DrawRect(rectBounds, paint);

            paint.Color = SKColors.Black;

            var text = (recursionItem.LevelIndex + 1).ToString();

            var textBounds = SKRect.Empty;
            paint.MeasureText(text, ref textBounds);

            textBounds.Offset(rectBounds.MidX - textBounds.MidX, rectBounds.MidY - textBounds.MidY);

            canvas.DrawText(text, textBounds.Left, textBounds.Bottom, paint);
        }
    }

    private void RenderNodes(SKCanvas canvas, SKPaint paint, SKShaper shaper)
    {
        foreach (var recursionItem in RecurseNodes(rootId))
        {
            var (node, element) = recursionItem.Item;

            var bounds = transformation.MapRect(element.Bounds);

            var shouldRenderNode = nodeVisibilityDictionary[node.NodeType];

            if (!shouldRenderNode)
            {
                continue;
            }

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

                RenderText(canvas, new SKPoint(bounds.MidX, bounds.MidY), paint, shaper, node.InnerText);
            }
            else
            {
                paint.IsStroke = false;
                paint.Color = node.IsSelected ? SKColors.Red.WithAlpha(16) : color.WithAlpha(16);

                canvas.DrawRect(bounds, paint);
            }

            paint.Color = node.IsSelected ? SKColors.Red : color;
            paint.IsStroke = true;

            canvas.DrawRect(bounds, paint);
        }
    }

    private void RenderText(SKCanvas canvas, SKPoint center, SKPaint paint, SKShaper shaper, string text)
    {
        if (paint.ContainsGlyphs(text.AsSpan()))
        {
            var textBounds = SKRect.Empty;

            paint.MeasureText(text, ref textBounds);

            var paraLevel = ViewModel?.Direction == Direction.Rtl ? BiDi.BiDiDirection.RTL : BiDi.BiDiDirection.LTR;

            bidi.SetPara(text, (byte)paraLevel, null);

            // DO_MIRRORING takes care of flipping characters like parentheses.
            text = bidi.GetReordered(BiDi.CallReorderingOptions.DO_MIRRORING);

            canvas.DrawText(
                text,
                center.X - textBounds.MidX,
                center.Y - textBounds.MidY,
                paint
            );

            return;
        }

        // In this case, we are missing some glyphs, so we will need to render text with a font fallback.
        RenderMultipleTypefaceText(canvas, center, paint, text);
    }

    private void RenderMultipleTypefaceText(
        SKCanvas canvas,
        SKPoint center,
        SKPaint paint,
        string text
    )
    {
        var originalTypeface = paint.Typeface;

        // The start index keeps track of the start of the current text run, while the cursor index allows skipping
        // chars if no font is found for them.
        // When inserting a new run, the start index will not have moved from the end of the previous text run, so
        // we include characters we may have skipped.
        var startIndex = 0;
        var cursorIndex = 0;

        var textBounds = SKRect.Empty;

        var list = new List<(int startIndex, int endIndexExclusive, SKTypeface typeface, SKRect runBounds)>();

        while (true)
        {
            var glyphs = paint.GetGlyphs(text.AsSpan()) ?? Array.Empty<ushort>();

            var endIndex = Array.IndexOf(glyphs, (ushort)0, cursorIndex);

            if (endIndex < 0)
            {
                endIndex = text.Length;
            }

            if (endIndex - cursorIndex > 0)
            {
                var runBounds = SKRect.Empty;

                paint.MeasureText(text, ref runBounds);

                runBounds.Offset(textBounds.Right, -runBounds.Top);

                // Keep track of the overall side of the text.
                textBounds.Right += runBounds.Width;
                textBounds.Bottom = Math.Max(Math.Abs(runBounds.Height), textBounds.Bottom);

                list.Add((startIndex, endIndex, paint.Typeface, runBounds));

                // Advance to the beginning of the next text run.
                startIndex = endIndex;
            }

            if (endIndex >= text.Length)
            {
                break;
            }

            cursorIndex = endIndex;

            var typeface = SKFontManager.Default.MatchCharacter(text[cursorIndex]);

            if (typeface == null)
            {
                // If we couldn't find a font for this character, skip over it. It will be shown as an unknown char.
                cursorIndex++;
            }
            else
            {
                paint.Typeface = typeface;
            }
        }

        foreach (var item in list)
        {
            paint.Typeface = item.typeface;

            var paraLevel = ViewModel?.Direction == Direction.Rtl ? BiDi.BiDiDirection.RTL : BiDi.BiDiDirection.LTR;

            bidi.SetPara(text, (byte)paraLevel, null);

            text = bidi.GetReordered(BiDi.CallReorderingOptions.DEFAULT);

            canvas.DrawText(
                text[item.startIndex..item.endIndexExclusive],
                center.X - textBounds.MidX + item.runBounds.Left,
                center.Y - textBounds.MidY + item.runBounds.Height,
                paint
            );
        }

        paint.Typeface = originalTypeface;
    }

    private void RenderBackground(SKCanvas canvas, SKPaint paint)
    {
        if (background == null)
        {
            return;
        }

        var bounds = new SKRect(0, 0, background.Width, background.Height);

        bounds = transformation.MapRect(bounds);

        var shouldRenderNode = nodeVisibilityDictionary[HocrNodeType.Page];

        if (shouldRenderNode)
        {
            canvas.DrawBitmap(background, bounds);
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

    private IEnumerable<RecursiveSelectHelper.RecursionItem<(HocrNodeViewModel, Element)>> RecurseNodes(int key) =>
        Enumerable
            .Repeat(elements[key], 1)
            .IndexedRecursiveSelect(tuple => tuple.Item1.Children.Select(c => elements[c.Id]));
}
