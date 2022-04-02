using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Helpers;
using HtmlAgilityPack;

namespace HocrEditor.Models
{
    public abstract record HocrNode
    {
        public static HocrNode FromHtmlNode(
            HtmlNode htmlNode,
            int id,
            int parentId,
            string language,
            Direction direction,
            IEnumerable<HocrNode> children
        )
        {
            var className = htmlNode.GetClasses().First();

            var title = HtmlEntity.DeEntitize(htmlNode.GetAttributeValue("title", string.Empty));

            if (parentId < 0)
            {
                return new HocrPage(id, title, language, direction, children);
            }

            return className switch
            {
                "ocr_image" or "ocr_photo" or "ocr_graphic" => new HocrImage(id, parentId, title, language, direction),
                "ocr_carea" => new HocrContentArea(id, parentId, title, language, direction, children),
                "ocr_par" => new HocrParagraph(
                    id,
                    parentId,
                    title,
                    language,
                    direction,
                    children
                )
                {
                    Direction = htmlNode.GetAttributeValue("dir", string.Empty) switch
                    {
                        "rtl" => Direction.Rtl,
                        _ => Direction.Ltr,
                    }
                },
                "ocr_line" => new HocrLine(id, parentId, title, language, direction, children),
                "ocrx_word" => new HocrWord(id, parentId, title, language, direction, htmlNode.InnerText.Trim('\u200E')), // Trim left-to-right marks.
                "ocr_textfloat" => new HocrTextFloat(id, parentId, title, language, direction, children),
                "ocr_caption" => new HocrCaption(id, parentId, title, language, direction, children),
                "ocr_header" => new HocrHeader(id, parentId, title, language, direction, children),
                "ocr_footer" => new HocrFooter(id, parentId, title, language, direction, children),
                _ => throw new ArgumentOutOfRangeException($"Unknown class name {className}")
            };
        }

        protected HocrNode(
            HocrNodeType nodeType,
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            IEnumerable<HocrNode> children
        )
        {
            NodeType = nodeType;
            Id = id;
            ParentId = parentId;
            Title = title;
            Language = language;
            Direction = direction;
            ChildNodes = children.ToList();
            BBox = Rect.FromBboxAttribute(GetAttributeFromTitle("bbox"));
        }

        public HocrNodeType NodeType { get; init; }
        public string Title { get; set; } = string.Empty;
        public int Id { get; set; }
        public int ParentId { get; set; }

        public Direction Direction { get; set; }

        public string Language { get; init; }

        public Rect BBox { get; set; }
        public List<HocrNode> ChildNodes { get; }

        public bool IsLineElement => HocrNodeTypeHelper.IsLineElement(NodeType);

        protected string GetAttributeFromTitle(string attribute)
        {
            var attributeValueIndex = Title.IndexOf($"{attribute} ", StringComparison.Ordinal);

            if (attributeValueIndex < 0)
            {
                return string.Empty;
            }

            var semicolonIndex = Title.IndexOf(';', attributeValueIndex);

            if (semicolonIndex == -1)
            {
                semicolonIndex = Title.Length;
            }

            return Title[(attributeValueIndex + attribute.Length + 1)..semicolonIndex];
        }
    }
}
