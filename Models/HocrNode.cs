using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HocrEditor.Services;
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

        protected HocrNode(HocrNodeType nodeType, HtmlNode node, string parentId)
        {
            var id = node.GetAttributeValue("id", string.Empty);
            var title = node.GetAttributeValue("title", string.Empty);

            var children = node.ChildNodes
                .Where(n => n.Name != "#text")
                .Select(htmlNode => HocrDocumentParser.ParseNode(htmlNode, id))
                .ToList();

            NodeType = nodeType;
            HtmlNodeType = node.Name;
            Title = title;
            Id = id;
            ParentId = parentId;
            InnerText = string.Empty;
            BBox = Rect.FromBboxAttribute(GetAttributeFromTitle("bbox"));
            ChildNodes = children;
        }


        public HocrNodeType NodeType { get; init; }

        public virtual HocrNodeType[] MatchingNodeTypes { get; } = Array.Empty<HocrNodeType>();
        public string HtmlNodeType { get; init; }
        public string Title { get; init; }
        public string Id { get; init; }
        public string ParentId { get; init; }
        public string InnerText { get; init; }
        public Rect BBox { get; init; }
        public IList<IHocrNode> ChildNodes { get; init; }
    }
}
