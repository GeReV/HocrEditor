using System.Linq;
using HocrEditor.Models;
using HtmlAgilityPack;

namespace HocrEditor.Services
{
    public class HocrDocumentParser
    {
        public HocrDocument Parse(HtmlDocument document)
        {
            var pageNode = document.DocumentNode.SelectSingleNode("//body/div[@class='ocr_page']");

            var page = Parse(pageNode, null);

            return new HocrDocument((HocrPage)page);
        }

        private IHocrNode Parse(HtmlNode node, string? parentId)
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
