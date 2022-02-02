using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Models;
using HtmlAgilityPack;

namespace HocrEditor.Services
{
    public class HocrPageParser
    {
        private int idCounter;

        public HocrPage Parse(HtmlDocument document)
        {
            var pageNode = document.DocumentNode.SelectSingleNode("//body/div[@class='ocr_page']");

            var page = (HocrPage)Parse(pageNode, -1, string.Empty, Direction.Ltr);

            var ocrSystem = document.DocumentNode.SelectSingleNode("//head/meta[@name='ocr-system']")
                .GetAttributeValue("content", string.Empty);

            page.OcrSystem = ocrSystem;

            var capabilities = document.DocumentNode.SelectSingleNode("//head/meta[@name='ocr-capabilities']")
                .GetAttributeValue("content", string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            page.Capabilities.AddRange(capabilities);

            return page;
        }

        private IHocrNode Parse(HtmlNode node, int parentId, string language, Direction direction)
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
