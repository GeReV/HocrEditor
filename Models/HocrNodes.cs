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
            Image = GetAttributeFromTitle("image").Trim('"');

            var dpi = GetAttributeFromTitle("scan_res")
                .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToArray();

            if (dpi.Length == 2)
            {
                Dpi = (dpi[0], dpi[1]);
            }
        }

        public (int, int) Dpi { get; }

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
        }
    }

    public record HocrParagraph : HocrNode
    {

        public HocrParagraph(string id, string? parentId, string title, IEnumerable<IHocrNode> children) : base(
            HocrNodeType.Paragraph,
            id,
            parentId,
            title,
            children
        )
        {
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
            Size = float.Parse(GetAttributeFromTitle("x_size"));
            Descenders = float.Parse(GetAttributeFromTitle("x_descenders"));
            Ascenders = float.Parse(GetAttributeFromTitle("x_ascenders"));

            var baseline = GetAttributeFromTitle("baseline")
                .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                .Select(float.Parse)
                .ToArray();

            if (baseline.Length == 2)
            {
                Baseline = (baseline[0], baseline[1]);
            }
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

    public sealed record HocrWord : HocrNode
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

            var fsize = GetAttributeFromTitle("x_fsize");
            if (!string.IsNullOrEmpty(fsize))
            {
                FontSize = int.Parse(fsize);
            }
        }

        public string InnerText { get; set; }

        public string? Language { get; init; }

        public Direction? Direction { get; init; }

        public float Confidence { get; }

        public int FontSize { get; }
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
        }
    }
}
