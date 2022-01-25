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

            var page = Parse(pageNode, null);

            return (HocrPage)page;
        }

        private static IHocrNode Parse(HtmlNode node, string? parentId)
        {
            var nodeId = node.GetAttributeValue("id", string.Empty);

            var children = node.ChildNodes
                .Where(childNode => childNode.Name != "#text")
                .Select(childNode => Parse(childNode, nodeId))
                .ToList();

            return HocrNode.FromHtmlNode(node, parentId, children);
        }
    }
}
