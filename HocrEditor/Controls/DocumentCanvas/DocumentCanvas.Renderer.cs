using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Threading;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using Icu;
using SkiaSharp;

namespace HocrEditor.Controls;

public partial class DocumentCanvas
{
    private readonly BiDi bidi = new();

    private void RenderCanvas(SKCanvas canvas)
    {
        using var font = new SKFont(SKTypeface.Default, 10.0f);
        using var paint = new SKPaint();

        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 0;

        RenderBackground(canvas, paint);

        if (RootId < 0)
        {
            return;
        }

        RenderNodes(canvas, paint, font);

        if (IsShowNumbering)
        {
            RenderNumbering(canvas, paint, font);
        }

        ActiveTool.Render(canvas);
    }

    private void RenderNumbering(SKCanvas canvas, SKPaint paint, SKFont font)
    {
        const float counterFontSize = 9.0f;
        var counterTextSize = counterFontSize / 72.0f * ((HocrPage)Elements[RootId].Item1.HocrNode).Dpi.Item2;

        // var stack = new Stack<int>();
        //
        // stack.Push(RootId);

        foreach (var recursionItem in RecurseNodes(RootId))
        {
            var (node, element) = recursionItem.Item;

            var shouldRenderNode = NodeVisibilityDictionary[node.NodeType];

            if (!shouldRenderNode)
            {
                continue;
            }

            var color = GetNodeColor(node);

            font.Size = counterTextSize;

            font.MeasureText("99", out var rectBounds);

            paint.IsStroke = false;
            paint.Color = color;

            rectBounds.Bottom += rectBounds.Height * 0.2f;
            rectBounds.Location = element.Bounds.Location;

            canvas.DrawRect(rectBounds, paint);

            paint.Color = SKColors.Black;

            var text = (recursionItem.LevelIndex + 1).ToString(new NumberFormatInfo());

            font.MeasureText(text, out var textBounds);

            textBounds.Offset(rectBounds.MidX - textBounds.MidX, rectBounds.MidY - textBounds.MidY);

            canvas.DrawText(text, textBounds.Left, textBounds.Bottom, SKTextAlign.Left, font, paint);
        }
    }

    private void RenderNodes(SKCanvas canvas, SKPaint paint, SKFont font)
    {
        foreach (var recursionItem in RecurseNodes(RootId))
        {
            var (node, element) = recursionItem.Item;

            var shouldRenderNode = NodeVisibilityDictionary[node.NodeType];

            if (!shouldRenderNode)
            {
                continue;
            }

            var color = GetNodeColor(node);

            if (node.NodeType == HocrNodeType.Word && IsShowText)
            {
                paint.IsStroke = false;
                paint.Color = SKColors.White.WithAlpha(128);

                canvas.DrawRect(element.Bounds, paint);

                var fontSize = ((HocrLine)Elements[node.ParentId].Item1.HocrNode).FontSize;

                font.Size = fontSize * 0.75f;

                paint.Color = SKColors.Black;
                paint.IsStroke = false;

                RenderText(canvas, new SKPoint(element.Bounds.MidX, element.Bounds.MidY), paint, font, node.InnerText);
            }
            else
            {
                paint.IsStroke = false;
                paint.Color = node.IsSelected ? SKColors.Red.WithAlpha(16) : color.WithAlpha(16);

                canvas.DrawRect(element.Bounds, paint);
            }

            paint.Color = node.IsSelected ? SKColors.Red : color;
            paint.IsStroke = true;

            canvas.DrawRect(element.Bounds, paint);
        }
    }

    private void RenderText(SKCanvas canvas, SKPoint center, SKPaint paint, SKFont font, string text)
    {
        if (font.ContainsGlyphs(text.AsSpan()))
        {
            font.MeasureText(text, out var textBounds);

            canvas.DrawText(
                ReorderBidirectionalText(text),
                center.X - textBounds.MidX,
                center.Y - textBounds.MidY,
                SKTextAlign.Left,
                font,
                paint
            );

            return;
        }

        // In this case, we are missing some glyphs, so we will need to render text with a font fallback.
        RenderMultipleTypefaceText(canvas, center, paint, font, text);
    }

    private void RenderMultipleTypefaceText(
        SKCanvas canvas,
        SKPoint center,
        SKPaint paint,
        SKFont font,
        string text
    )
    {
        var originalTypeface = font.Typeface;

        // The start index keeps track of the start of the current text run, while the cursor index allows skipping
        // chars if no font is found for them.
        // When inserting a new run, the start index will not have moved from the end of the previous text run, so
        // we include characters we may have skipped.
        var startIndex = 0;
        var cursorIndex = 0;

        var textBounds = SKRect.Empty;

        var list = new List<(int startIndex, int endIndexExclusive, SKTypeface typeface, SKRect runBounds)>();

        text = ReorderBidirectionalText(text);

        while (true)
        {
            var glyphs = font.GetGlyphs(text.AsSpan()) ?? Array.Empty<ushort>();

            var endIndex = Array.IndexOf(glyphs, (ushort)0, cursorIndex);

            if (endIndex < 0)
            {
                endIndex = text.Length;
            }

            if (endIndex - cursorIndex > 0)
            {
                font.MeasureText(text, out var runBounds);

                runBounds.Offset(textBounds.Right, -runBounds.Top);

                // Keep track of the overall side of the text.
                textBounds.Right += runBounds.Width;
                textBounds.Bottom = Math.Max(Math.Abs(runBounds.Height), textBounds.Bottom);

                list.Add((startIndex, endIndex, font.Typeface, runBounds));

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
                font.Typeface = typeface;
            }
        }

        foreach (var item in list)
        {
            font.Typeface = item.typeface;

            canvas.DrawText(
                text[item.startIndex..item.endIndexExclusive],
                center.X - textBounds.MidX + item.runBounds.Left,
                center.Y - textBounds.MidY + item.runBounds.Height,
                SKTextAlign.Left,
                font,
                paint
            );
        }

        font.Typeface = originalTypeface;
    }

    private void RenderBackground(SKCanvas canvas, SKPaint paint)
    {
        if (background == null)
        {
            return;
        }

        var backgroundTask = background.GetBitmap();

        if (!backgroundTask.IsCompleted)
        {
            // If the background isn't available yet, schedule another render for when it is available and skip it for now.
            _ = backgroundTask.ContinueWith(
                task =>
                {
                    if (task.IsCanceled)
                    {
                        return;
                    }

                    Dispatcher.Invoke(Refresh, DispatcherPriority.Render);
                },
                backgroundLoadCancellationTokenSource.Token
            );

            return;
        }

        var image = backgroundTask.Result;

        var bounds = new SKRect(0, 0, image.Width, image.Height);

        var shouldRenderNode = NodeVisibilityDictionary[HocrNodeType.Page];

        if (shouldRenderNode)
        {
            canvas.DrawBitmap(image, bounds);
        }
        else
        {
            paint.Style = SKPaintStyle.Fill;
            paint.Color = SKColors.White;

            canvas.DrawRect(bounds, paint);
        }

        paint.Color = SKColors.Gray;
        paint.Style = SKPaintStyle.Stroke;

        canvas.DrawRect(bounds, paint);
    }

    private string ReorderBidirectionalText(string text)
    {
        var paraLevel = ViewModel?.Direction == Direction.Rtl ? BiDi.BiDiDirection.RTL : BiDi.BiDiDirection.LTR;

        bidi.SetPara(text, (byte)paraLevel, null);

        // DO_MIRRORING takes care of flipping characters like parentheses.
        text = bidi.GetReordered(BiDi.CallReorderingOptions.DO_MIRRORING);
        return text;
    }

    private IEnumerable<RecursiveSelectHelper.RecursionItem<(HocrNodeViewModel, CanvasElement)>>
        RecurseNodes(int key) =>
        Enumerable
            .Repeat(Elements[key], 1)
            .IndexedRecursiveSelect(tuple => tuple.Item1.Children.Select(c => Elements[c.Id]));
}
