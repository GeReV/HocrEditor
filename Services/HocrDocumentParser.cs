using System;
using System.Linq;
using HocrEditor.Models;
using HtmlAgilityPack;

namespace HocrEditor.Services
{
    public class HocrDocumentParser
    {
        public static HocrDocument? Parse(HtmlDocument document)
        {
            var pageNode = document.DocumentNode.SelectSingleNode("//body/div[@class='ocr_page']");

            return new HocrDocument(new HocrPage(pageNode));
        }

        public static IHocrNode ParseNode(HtmlNode node, string parentId)
        {
            var className = node.GetClasses().First();

            return className switch
            {
                "ocr_graphic" => new HocrGraphic(node, parentId),
                "ocr_carea" => new HocrContentArea(node, parentId),
                "ocr_par" => new HocrParagraph(node, parentId),
                "ocr_line" => new HocrLine(node, parentId),
                "ocrx_word" => new HocrWord(node, parentId),
                "ocr_textfloat" => new HocrTextFloat(node, parentId),
                "ocr_caption" => new HocrCaption(node, parentId),
                _ => throw new ArgumentOutOfRangeException($"Unknown class name {className}")
            };
        }
    }
}
