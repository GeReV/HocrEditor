using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace HocrEditor.Models
{
    public abstract record HocrNode : IHocrNode
    {
        protected static readonly string ParagraphSeparator = Environment.NewLine + Environment.NewLine;

        protected static string JoinInnerText(string separator, IEnumerable<IHocrNode> nodes) =>
            string.Join(separator, nodes.Select(n => n.InnerText));

        protected string GetAttributeFromTitle(string attribute)
        {
            var attributeValueIndex = Title.IndexOf($"{attribute} ", StringComparison.Ordinal);
            var semicolonIndex = Title.IndexOf(';', attributeValueIndex);

            Debug.Assert(attributeValueIndex >= 0);

            if (semicolonIndex == -1)
            {
                semicolonIndex = Title.Length;
            }

            return Title[(attributeValueIndex + attribute.Length + 1)..semicolonIndex];
        }

        protected HocrNode(
            HocrNodeType nodeType,
            string id,
            string? parentId,
            string title,
            IEnumerable<IHocrNode> children
        )
        {
            NodeType = nodeType;
            Id = id;
            ParentId = parentId;
            Title = title;
            ChildNodes = children.ToList();
            BBox = Rect.FromBboxAttribute(GetAttributeFromTitle("bbox"));
        }

        public static HocrNode FromHtmlNode(HtmlNode htmlNode, string? parentId, IEnumerable<IHocrNode> children)
        {
            var className = htmlNode.GetClasses().First();

            var language = htmlNode.GetAttributeValue("lang", string.Empty);
            Direction? direction = htmlNode.GetAttributeValue("dir", string.Empty) switch
            {
                "ltr" => Direction.Ltr,
                "rtl" => Direction.Rtl,
                _ => null
            };

            var id = htmlNode.GetAttributeValue("id", string.Empty);
            var title = htmlNode.GetAttributeValue("title", string.Empty);

            HocrNode node = className switch
            {
                "ocr_page" => new HocrPage(id, title, children),
                "ocr_image" or "ocr_photo" or "ocr_graphic" => new HocrImage(id, parentId, title),
                "ocr_carea" => new HocrContentArea(id, parentId, title, children),
                "ocr_par" => new HocrParagraph(id, parentId, title, children)
                {
                    Language = language,
                    Direction = direction
                },
                "ocr_line" => new HocrLine(id, parentId, title, children),
                "ocrx_word" => new HocrWord(id, parentId, title, htmlNode.InnerText)
                {
                    Language = language,
                    Direction = direction
                },
                "ocr_textfloat" => new HocrTextFloat(id, parentId, title, children),
                "ocr_caption" => new HocrCaption(id, parentId, title, children),
                _ => throw new ArgumentOutOfRangeException($"Unknown class name {className}")
            };

            return node;
        }

        public HocrNodeType NodeType { get; init; }
        public virtual HocrNodeType[] MatchingNodeTypes { get; } = Array.Empty<HocrNodeType>();
        public string Title { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public string InnerText { get; set; } = string.Empty;
        public Rect BBox { get; set; }
        public IList<IHocrNode> ChildNodes { get; init; }
    }
}
