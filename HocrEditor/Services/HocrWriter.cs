using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HocrEditor.Core;
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

    private readonly Dictionary<string, uint> nodeCounters = new(StringComparer.Ordinal);

    public HocrWriter(HocrDocumentViewModel hocrDocumentViewModel, string filename)
    {
        this.hocrDocumentViewModel = hocrDocumentViewModel;
        this.filename = filename;
    }

    public HtmlDocument Build()
    {
        var html = document.CreateElement("html");

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
            .Distinct(StringComparer.Ordinal)
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

        head.AppendChild(
            document.CreateMeta(
                "ocr-number-of-pages",
                hocrDocumentViewModel.Pages.Count.ToString(new NumberFormatInfo())
            )
        );

        head.AppendChild(document.CreateElement("title"));

        return head;
    }

    private static List<Script> GetScripts(List<string> languages) =>
        languages.Where(lang => lang.StartsWith(SCRIPT_PREFIX, StringComparison.Ordinal))
            .Select(script => Script.FromName(script.Remove(0, SCRIPT_PREFIX.Length), true))
            .OfType<Script>()
            .ToList();

    private static List<Language> GetLanguages(IEnumerable<string> languages) =>
        languages
            .Select(
                lang =>
                {
                    if (lang.StartsWith(SCRIPT_PREFIX, StringComparison.Ordinal))
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

            Ensure.IsNotNull(page.HocrPage);

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

        Ensure.IsNotNull(hocrNode);

        var node = document.CreateElement(
            HocrNodeTypeHelper.ElementTypeForType(hocrNode.NodeType)
        );

        node.AddClass(HocrNodeTypeHelper.HocrClassNameForType(hocrNode.NodeType));

        var nodeId = HocrNodeTypeHelper.HocrIdForType(hocrNode.NodeType);

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

        if (hocrNode.Direction != currentDirection || hocrNode.NodeType == HocrNodeType.Page)
        {
            currentDirection = hocrNode.Direction;

            node.SetAttributeValue("dir", hocrNode.Direction == Direction.Ltr ? "ltr" : "rtl");
        }

        if (!string.Equals(hocrNode.Language, currentLanguage, StringComparison.Ordinal))
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

    private string BuildTitle(HocrNode hocrNode, int pageIndex)
    {
        var sb = new StringBuilder();

        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "bbox {0} {1} {2} {3}",
            hocrNode.BBox.Left,
            hocrNode.BBox.Top,
            hocrNode.BBox.Right,
            hocrNode.BBox.Bottom
        );

        switch (hocrNode)
        {
            case HocrPage hocrPage:
                var relativeImagePath = Path.GetRelativePath(
                    Path.GetDirectoryName(filename) ?? string.Empty,
                    hocrPage.ImageFilename
                );

                sb.AppendFormat(CultureInfo.InvariantCulture, "; image \"{0}\"", relativeImagePath);
                sb.AppendFormat(CultureInfo.InvariantCulture, "; ppageno {0}", pageIndex);
                sb.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "; scan_res {0} {1}",
                    hocrPage.Dpi.Item1,
                    hocrPage.Dpi.Item2
                );
                break;
            case HocrLine hocrLine: // Also Caption and TextFloat.
                sb.AppendFormat(
                    CultureInfo.InvariantCulture,
                    "; baseline {0} {1}",
                    hocrLine.Baseline.Item1,
                    hocrLine.Baseline.Item2
                );
                sb.AppendFormat(CultureInfo.InvariantCulture, "; x_fsize {0}", hocrLine.FontSize);
                break;
            case HocrWord hocrWord:
                sb.AppendFormat(CultureInfo.InvariantCulture, "; x_wconf {0}", hocrWord.Confidence);
                break;
            case HocrContentArea:
            case HocrImage:
                // No special treatment.
                break;
        }

        return sb.ToString();
    }
}
