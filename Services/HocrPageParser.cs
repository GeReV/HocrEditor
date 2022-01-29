using System.Collections.Generic;
using System.Linq;
using HocrEditor.Models;
using HtmlAgilityPack;

namespace HocrEditor.Services
{
    public class HocrPageParser
    {
        public HocrPage Parse(HtmlDocument document)
        {
            var pageNode = document.DocumentNode.SelectSingleNode("//body/div[@class='ocr_page']");

            var page = Parse(pageNode, null, string.Empty, Direction.Ltr);

            return (HocrPage)page;
        }

        private static IHocrNode Parse(HtmlNode node, string? parentId, string language, Direction direction)
        {
            var nodeId = node.GetAttributeValue("id", string.Empty);

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

            return HocrNode.FromHtmlNode(node, parentId, language, direction, children);
        }
    }
}
