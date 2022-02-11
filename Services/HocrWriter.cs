using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HocrEditor.Core.Iso15924;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.ViewModels;
using HtmlAgilityPack;
using Iso639;

namespace HocrEditor.Services;

public class HocrWriter
{
    private const string SCRIPT_PREFIX = "script/";

    private readonly HocrDocumentViewModel hocrDocumentViewModel;
    private readonly string filename;

    private readonly HtmlDocument document = new();

    private readonly Dictionary<string, uint> nodeCounters = new();

    public HocrWriter(HocrDocumentViewModel hocrDocumentViewModel, string filename)
    {
        this.hocrDocumentViewModel = hocrDocumentViewModel;
        this.filename = filename;
    }

    public HtmlDocument Build()
    {
        var html = document.CreateElement("html");

        // TODO: Change this value based on common language or just leave as-is?
        html.SetAttributeValue("lang", "en");

        var commonDirection = hocrDocumentViewModel.Pages.CountBy(page => page.Direction).MaxBy(pair => pair.Value).Key;
        if (commonDirection == Direction.Rtl)
        {
            html.SetAttributeValue("dir", "rtl");
        }

        html.AppendChild(CreateHead());
        html.AppendChild(CreateBody());

        document.DocumentNode.AppendChild(document.CreateComment("<!DOCTYPE html>"));
        document.DocumentNode.AppendChild(html);

        return document;
    }

    private HtmlNode CreateHead()
    {
        var head = document.CreateElement("head");

        head.AppendChild(document.CreateMeta("ocr-system", hocrDocumentViewModel.OcrSystem));

        if (hocrDocumentViewModel.Capabilities.Any())
        {
            head.AppendChild(
                document.CreateMeta("ocr-capabilities", string.Join(' ', hocrDocumentViewModel.Capabilities))
            );
        }

        var allLanguages = hocrDocumentViewModel.Pages
            .SelectMany(page => page.Nodes.Select(n => n.HocrNode.Language))
            .Distinct()
            .ToList();

        var languages = GetLanguages(allLanguages);

        if (languages.Any())
        {
            head.AppendChild(document.CreateMeta("ocr-langs", string.Join(' ', languages.Select(l => l.Part1))));
        }

        var scripts = GetScripts(allLanguages);

        if (scripts.Any())
        {
            head.AppendChild(document.CreateMeta("ocr-scripts", string.Join(' ', scripts.Select(s => s.Code))));
        }

        head.AppendChild(document.CreateMeta("ocr-number-of-pages", hocrDocumentViewModel.Pages.Count.ToString()));

        head.AppendChild(document.CreateElement("title"));

        return head;
    }

    private static List<Script> GetScripts(List<string> languages) =>
        languages.Where(lang => lang.StartsWith(SCRIPT_PREFIX))
            .Select(script => Core.Iso15924.Script.FromName(script.Remove(0, SCRIPT_PREFIX.Length), true))
            .OfType<Script>()
            .ToList();

    private static List<Language> GetLanguages(IEnumerable<string> languages) =>
        languages
            .Select(
                lang =>
                {
                    if (lang.StartsWith(SCRIPT_PREFIX))
                    {
                        var script = Script.FromName(lang.Remove(0, SCRIPT_PREFIX.Length), true);

                        if (script == null)
                        {
                            return null;
                        }

                        return Language
                            .FromName(script.Name, true)
                            .FirstOrDefault(l => l.Type == LanguageType.Living); // TODO: Find a better ranking method.
                    }

                    return Language.FromPart3(lang);
                }
            )
            .OfType<Language>()
            .ToList();

    private HtmlNode CreateBody()
    {
        var body = document.CreateElement("body");

        for (var index = 0; index < hocrDocumentViewModel.Pages.Count; index++)
        {
            var page = hocrDocumentViewModel.Pages[index];

            ArgumentNullException.ThrowIfNull(page.HocrPage);

            body.AppendChild(
                CreateNode(
                    index,
                    page.Nodes.First(n => n.IsRoot),
                    page.HocrPage.Direction,
                    page.HocrPage.Language
                )
            );
        }

        return body;
    }

    private HtmlNode CreateNode(
        int pageIndex,
        HocrNodeViewModel hocrNodeViewModel,
        Direction currentDirection,
        string currentLanguage
    )
    {
        var hocrNode = hocrNodeViewModel.HocrNode;

        ArgumentNullException.ThrowIfNull(hocrNode);

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
            node.AppendChild(document.CreateTextNode(HtmlEntity.Entitize(hocrWord.InnerText, true, true)));
        }
        else
        {
            foreach (var childNode in hocrNodeViewModel.Children)
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
