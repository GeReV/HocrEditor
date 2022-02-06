using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HtmlAgilityPack;

namespace HocrEditor.Services;

public class HocrWriter
{
    private readonly HocrDocument hocrDocument;
    private readonly string filename;

    private readonly HtmlDocument document = new();

    private readonly Dictionary<string, uint> nodeCounters = new();

    public HocrWriter(HocrDocument hocrDocument, string filename)
    {
        this.hocrDocument = hocrDocument;
        this.filename = filename;
    }

    public HtmlDocument Build()
    {
        var html = document.CreateElement("html");

        html.AppendChild(CreateHead());
        html.AppendChild(CreateBody());

        document.DocumentNode.AppendChild(document.CreateComment("<!DOCTYPE html>"));
        document.DocumentNode.AppendChild(html);

        return document;
    }

    private HtmlNode CreateHead()
    {
        var head = document.CreateElement("head");

        head.AppendChild(document.CreateMeta("ocr-system", hocrDocument.OcrSystem));

        if (hocrDocument.Capabilities.Any())
        {
            head.AppendChild(
                document.CreateMeta("ocr-capabilities", string.Join(' ', hocrDocument.Capabilities))
            );
        }

        // TODO
        // head.AppendChild(document.CreateMeta("ocr-langs", ""));

        head.AppendChild(document.CreateMeta("ocr-number-of-pages", hocrDocument.Pages.Count.ToString()));

        head.AppendChild(document.CreateElement("title"));

        return head;
    }

    private HtmlNode CreateBody()
    {
        var body = document.CreateElement("body");

        for (var index = 0; index < hocrDocument.Pages.Count; index++)
        {
            var page = hocrDocument.Pages[index];

            body.AppendChild(CreateNode(index, page, page.Direction, page.Language));
        }

        return body;
    }

    private HtmlNode CreateNode(int pageIndex, IHocrNode hocrNode, Direction currentDirection, string currentLanguage)
    {
        var node = document.CreateElement(
            hocrNode switch
            {
                _ when hocrNode.NodeType is HocrNodeType.Page or HocrNodeType.ContentArea => "div",
                _ when hocrNode.NodeType is HocrNodeType.Paragraph => "p",
                _ when hocrNode.IsLineElement => "span",
                _ when hocrNode.NodeType is HocrNodeType.Word => "span",
                _ when hocrNode.NodeType is HocrNodeType.Image => "div",
                _ => throw new ArgumentOutOfRangeException()
            }
        );

        node.AddClass(
            hocrNode.NodeType switch
            {
                HocrNodeType.Page => "ocr_page",
                HocrNodeType.ContentArea => "ocr_carea",
                HocrNodeType.Paragraph => "ocr_par",
                HocrNodeType.Line => "ocr_line",
                HocrNodeType.Header => "ocr_header",
                HocrNodeType.Footer => "ocr_footer",
                HocrNodeType.TextFloat => "ocr_textfloat",
                HocrNodeType.Caption => "ocr_caption",
                HocrNodeType.Word => "ocrx_word",
                HocrNodeType.Image => "ocr_image",
                _ => throw new ArgumentOutOfRangeException()
            }
        );

        var nodeId = hocrNode switch
        {
            _ when hocrNode.IsLineElement => "line",
            _ when hocrNode.NodeType is HocrNodeType.Page => "page",
            _ when hocrNode.NodeType is HocrNodeType.ContentArea => "block",
            _ when hocrNode.NodeType is HocrNodeType.Paragraph => "par",
            _ when hocrNode.NodeType is HocrNodeType.Word => "word",
            _ when hocrNode.NodeType is HocrNodeType.Image => "image",
            _ => throw new ArgumentOutOfRangeException()
        };

        var id = $"{nodeId}_{pageIndex + 1}";

        if (hocrNode.NodeType != HocrNodeType.Page)
        {
            if (nodeCounters.TryGetValue(nodeId, out var nodeNumber))
            {
                nodeNumber += 1;
            }
            else
            {
                nodeNumber = 1;
            }

            nodeCounters[nodeId] = nodeNumber;

            id += $"_{nodeNumber}";
        }

        node.SetAttributeValue("id", id);
        node.SetAttributeValue("title", BuildTitle(hocrNode, pageIndex));

        if (hocrNode.Direction != currentDirection)
        {
            currentDirection = hocrNode.Direction;

            node.SetAttributeValue("dir", hocrNode.Direction == Direction.Ltr ? "ltr" : "rtl");
        }

        if (hocrNode.Language != currentLanguage)
        {
            currentLanguage = hocrNode.Language;

            node.SetAttributeValue("lang", hocrNode.Language);
        }

        if (hocrNode is HocrWord hocrWord)
        {
            node.AppendChild(document.CreateTextNode(hocrWord.InnerText));
        }
        else
        {
            foreach (var childNode in hocrNode.ChildNodes)
            {
                node.AppendChild(CreateNode(pageIndex, childNode, currentDirection, currentLanguage));
            }
        }

        return node;
    }

    private string BuildTitle(IHocrNode hocrNode, int pageIndex)
    {
        var sb = new StringBuilder();

        sb.Append($"bbox {hocrNode.BBox.Left} {hocrNode.BBox.Top} {hocrNode.BBox.Right} {hocrNode.BBox.Bottom}");

        switch (hocrNode)
        {
            case HocrPage hocrPage:
                var relativeImagePath = Path.GetRelativePath(
                    Path.GetDirectoryName(filename) ?? string.Empty,
                    hocrPage.Image
                );

                sb.Append($"; image \"{relativeImagePath}\"");
                sb.Append($"; ppageno {pageIndex}");
                sb.Append($"; scan_res {hocrPage.Dpi.Item1} {hocrPage.Dpi.Item2}");
                break;
            case HocrLine hocrLine: // Also Caption and TextFloat.
                sb.Append($"; baseline {hocrLine.Baseline.Item1} {hocrLine.Baseline.Item2}");
                sb.Append($"; x_fsize {hocrLine.FontSize}");
                break;
            case HocrWord hocrWord:
                sb.Append($"; x_wconf {hocrWord.Confidence}");
                break;
            case HocrContentArea:
            case HocrImage:
                // No special treatment.
                break;
        }

        return sb.ToString();
    }
}
