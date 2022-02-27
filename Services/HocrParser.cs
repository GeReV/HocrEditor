using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HocrEditor.Models;
using HtmlAgilityPack;

namespace HocrEditor.Services
{
    public class HocrParser
    {
        private int idCounter;

        public HocrDocument Parse(string filename)
        {
            var stream = File.OpenRead(filename);

            var doc = new HtmlDocument();
            doc.Load(stream);

            var hocrDocument = Parse(doc);

            foreach (var page in hocrDocument.Pages)
            {
                page.Image = Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty, page.Image);
            }

            return hocrDocument;
        }

        public HocrDocument Parse(HtmlDocument document)
        {
            var html = document.DocumentNode.SelectSingleNode("//html");

            var htmlDirection = html.GetAttributeValue("dir", "ltr") == "rtl" ? Direction.Rtl : Direction.Ltr;

            var pageNodes = document.DocumentNode.SelectNodes("//body/div[@class='ocr_page']");

            var pages = new List<HocrPage>();

            foreach (var pageNode in pageNodes)
            {
                idCounter = 0;

                var page = (HocrPage)Parse(pageNode, -1, string.Empty, htmlDirection);

                pages.Add(page);
            }

            var hocrDocument = new HocrDocument(pages);

            var ocrSystem = document.DocumentNode.SelectSingleNode("//head/meta[@name='ocr-system']")
                .GetAttributeValue("content", string.Empty);

            hocrDocument.OcrSystem = ocrSystem;

            var capabilities = document.DocumentNode.SelectSingleNode("//head/meta[@name='ocr-capabilities']")
                .GetAttributeValue("content", string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            hocrDocument.Capabilities.AddRange(capabilities);

            return hocrDocument;
        }

        private HocrNode Parse(HtmlNode node, int parentId, string language, Direction direction)
        {
            var nodeId = idCounter++;

            language = node.GetAttributeValue("lang", language);

            direction = node.GetAttributeValue("dir", string.Empty) switch
            {
                "rtl" => Direction.Rtl,
                "ltr" => Direction.Ltr,
                _ => direction,
            };

            var children = node.ChildNodes
                .Where(childNode => childNode.Name != "#text")
                .Select(childNode => Parse(childNode, nodeId, language, direction))
                .ToList();

            return HocrNode.FromHtmlNode(node, nodeId, parentId, language, direction, children);
        }
    }
}
