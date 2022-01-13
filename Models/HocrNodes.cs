using System;
using System.Collections.Generic;
using System.Linq;
using HtmlAgilityPack;

namespace HocrEditor.Models
{
    public enum Direction
    {
        Ltr,
        Rtl
    }

    public record HocrPage : HocrNode
    {
        public HocrPage(HtmlNode node, IEnumerable<IHocrNode> children) : base(HocrNodeType.Page, node, string.Empty, children)
        {
            InnerText = JoinInnerText(ParagraphSeparator, ChildNodes);
            Image = GetAttributeFromTitle("image").Trim('"');
        }

        public string Image { get; }
    }

    public record HocrContentArea : HocrNode
    {
        public HocrContentArea(HtmlNode node, string? parentId, IEnumerable<IHocrNode> children) : base(HocrNodeType.ContentArea, node, parentId, children)
        {
            InnerText = JoinInnerText(ParagraphSeparator, ChildNodes);
        }
    }

    public record HocrParagraph : HocrNode
    {
        private static readonly string LineSeparator = Environment.NewLine;

        public HocrParagraph(HtmlNode node, string? parentId, IEnumerable<IHocrNode> children) : base(HocrNodeType.Paragraph, node, parentId, children)
        {
            InnerText = JoinInnerText(LineSeparator, ChildNodes);
            Language = node.GetAttributeValue("lang", string.Empty);
            Direction = node.GetAttributeValue("dir", string.Empty) switch
            {
                "ltr" => Models.Direction.Ltr,
                "rtl" => Models.Direction.Rtl,
                _ => null,
            };
        }

        public string Language { get; }

        public Direction? Direction { get; }
    }

    public record HocrLine : HocrNode
    {
        public HocrLine(HtmlNode node, string? parentId, IEnumerable<IHocrNode> children) : base(HocrNodeType.Line, node, parentId, children)
        {
            InnerText = JoinInnerText(" ", ChildNodes);

            var baseline = GetAttributeFromTitle("baseline")
                .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                .Select(float.Parse)
                .ToArray();
            Baseline = (baseline[0], baseline[1]);

            Size = float.Parse(GetAttributeFromTitle("x_size"));
            Descenders = float.Parse(GetAttributeFromTitle("x_descenders"));
            Ascenders = float.Parse(GetAttributeFromTitle("x_ascenders"));
        }

        public override HocrNodeType[] MatchingNodeTypes { get; } = {
            HocrNodeType.Line,
            HocrNodeType.Caption,
            HocrNodeType.TextFloat,
        };

        public (float, float) Baseline { get; }
        public float Size { get; }
        public float Descenders { get; }
        public float Ascenders { get; }
    }

    public record HocrTextFloat : HocrLine
    {
        public HocrTextFloat(HtmlNode node, string? parentId, IEnumerable<IHocrNode> children) : base(node, parentId, children)
        {
            NodeType = HocrNodeType.TextFloat;
        }
    }

    public record HocrCaption : HocrLine
    {
        public HocrCaption(HtmlNode node, string? parentId, IEnumerable<IHocrNode> children) : base(node, parentId, children)
        {
            NodeType = HocrNodeType.Caption;
        }
    }

    public record HocrWord : HocrNode
    {
        public HocrWord(HtmlNode node, string? parentId) : base(HocrNodeType.Word, node, parentId, Enumerable.Empty<IHocrNode>())
        {
            InnerText = HtmlEntity.DeEntitize(node.InnerText.Trim());
            Language = node.GetAttributeValue("lang", string.Empty);
            Direction = node.GetAttributeValue("dir", string.Empty) switch
            {
                "ltr" => Models.Direction.Ltr,
                "rtl" => Models.Direction.Rtl,
                _ => null,
            };
            Confidence = int.Parse(GetAttributeFromTitle("x_wconf"));
        }

        public string Language { get; }

        public Direction? Direction { get; }

        public float Confidence { get; }
    }

    public record HocrGraphic : HocrNode
    {
        private readonly int imageId;

        public HocrGraphic(HtmlNode node, string? parentId, int imageId) : base(HocrNodeType.Graphic, node, parentId, Enumerable.Empty<IHocrNode>())
        {
            this.imageId = imageId;

            InnerText = $"Graphic {imageId}";
        }
    }
}
