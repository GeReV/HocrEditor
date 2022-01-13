using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HocrEditor.Models;
using HtmlAgilityPack;

namespace HocrEditor.Services
{
    public class HocrDocumentParser
    {
        private int imageCounter;

        public HocrDocument Parse(HtmlDocument document)
        {
            var pageNode = document.DocumentNode.SelectSingleNode("//body/div[@class='ocr_page']");

            var page = Parse(pageNode, null);

            return new HocrDocument(page);
        }

        private IHocrNode Parse(HtmlNode node, string? parentId)
        {
            var nodeId = node.GetAttributeValue("id", string.Empty);

            var children = node.ChildNodes
                .Where(childNode => childNode.Name != "#text")
                .Select(childNode => Parse(childNode, nodeId))
                .ToList();

            return ParseNode(node, parentId, children);
        }

        private IHocrNode ParseNode(HtmlNode node, string? parentId, IEnumerable<IHocrNode> children)
        {
            var className = node.GetClasses().First();

            return className switch
            {
                "ocr_page" => new HocrPage(node, children),
                "ocr_graphic" => new HocrGraphic(node, parentId, ++imageCounter),
                "ocr_carea" => new HocrContentArea(node, parentId, children),
                "ocr_par" => new HocrParagraph(node, parentId, children),
                "ocr_line" => new HocrLine(node, parentId, children),
                "ocrx_word" => new HocrWord(node, parentId),
                "ocr_textfloat" => new HocrTextFloat(node, parentId, children),
                "ocr_caption" => new HocrCaption(node, parentId, children),
                _ => throw new ArgumentOutOfRangeException($"Unknown class name {className}")
            };
        }
    }
}
