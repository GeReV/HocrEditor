using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace HocrEditor.Models
{
    public abstract record HocrNode : IHocrNode
    {
        public static HocrNode FromHtmlNode(
            HtmlNode htmlNode,
            int id,
            int parentId,
            string language,
            Direction direction,
            IEnumerable<IHocrNode> children
        )
        {
            var className = htmlNode.GetClasses().First();

            var title = htmlNode.GetAttributeValue("title", string.Empty);

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
                "ocrx_word" => new HocrWord(id, parentId, title, language, direction, htmlNode.InnerText),
                "ocr_textfloat" => new HocrTextFloat(id, parentId, title, language, direction, children),
                "ocr_caption" => new HocrCaption(id, parentId, title, language, direction, children),
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
            IEnumerable<IHocrNode> children
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
        public virtual HocrNodeType[] MatchingNodeTypes { get; } = Array.Empty<HocrNodeType>();
        public string Title { get; set; } = string.Empty;
        public int Id { get; set; }
        public int ParentId { get; set; }

        public Direction Direction { get; init; }

        public string Language { get; init; }

        public Rect BBox { get; set; }
        public List<IHocrNode> ChildNodes { get; }

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
