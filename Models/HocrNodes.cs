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
        public HocrPage(string id, string title, IEnumerable<IHocrNode> children) : base(
            HocrNodeType.Page,
            id,
            string.Empty,
            title,
            children
        )
        {
            InnerText = JoinInnerText(ParagraphSeparator, ChildNodes);
            Image = GetAttributeFromTitle("image").Trim('"');
        }

        public string Image { get; }
    }

    public record HocrContentArea : HocrNode
    {
        public HocrContentArea(string id, string? parentId, string title, IEnumerable<IHocrNode> children) : base(
            HocrNodeType.ContentArea,
            id,
            parentId,
            title,
            children
        )
        {
            InnerText = JoinInnerText(ParagraphSeparator, ChildNodes);
        }
    }

    public record HocrParagraph : HocrNode
    {
        private static readonly string LineSeparator = Environment.NewLine;

        public HocrParagraph(string id, string? parentId, string title, IEnumerable<IHocrNode> children) : base(
            HocrNodeType.Paragraph,
            id,
            parentId,
            title,
            children
        )
        {
            InnerText = JoinInnerText(LineSeparator, ChildNodes);
        }

        public string? Language { get; init; }

        public Direction? Direction { get; init; }
    }

    public record HocrLine : HocrNode
    {
        public HocrLine(string id, string? parentId, string title, IEnumerable<IHocrNode> children) : base(
            HocrNodeType.Line,
            id,
            parentId,
            title,
            children
        )
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

        public override HocrNodeType[] MatchingNodeTypes { get; } =
        {
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
        public HocrTextFloat(string id, string? parentId, string title, IEnumerable<IHocrNode> children) : base(
            id,
            parentId,
            title,
            children
        )
        {
            NodeType = HocrNodeType.TextFloat;
        }
    }

    public record HocrCaption : HocrLine
    {
        public HocrCaption(string id, string? parentId, string title, IEnumerable<IHocrNode> children) : base(
            id,
            parentId,
            title,
            children
        )
        {
            NodeType = HocrNodeType.Caption;
        }
    }

    public record HocrWord : HocrNode
    {
        public HocrWord(string id, string? parentId, string title, string innerText) : base(
            HocrNodeType.Word,
            id,
            parentId,
            title,
            Enumerable.Empty<IHocrNode>()
        )
        {
            InnerText = HtmlEntity.DeEntitize(innerText.Trim());
            Confidence = int.Parse(GetAttributeFromTitle("x_wconf"));
        }

        public string? Language { get; init; }

        public Direction? Direction { get; init; }

        public float Confidence { get; }
    }

    public record HocrImage : HocrNode
    {
        public HocrImage(string id, string? parentId, string title) : base(
            HocrNodeType.Image,
            id,
            parentId,
            title,
            Enumerable.Empty<IHocrNode>()
        )
        {
            InnerText = "Image";
        }
    }
}
