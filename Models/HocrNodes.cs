using System;
using System.Collections.Generic;
using System.Linq;
using HocrEditor.Helpers;
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
        public HocrPage(
            int id,
            string title,
            string language,
            Direction direction,
            IEnumerable<IHocrNode> children
        ) : base(
            HocrNodeType.Page,
            id,
            -1,
            title,
            language,
            direction,
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

        public IEnumerable<IHocrNode> Items => ChildNodes.RecursiveSelect(n => n.ChildNodes);
    }

    public record HocrContentArea : HocrNode
    {
        public HocrContentArea(
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            IEnumerable<IHocrNode> children
        ) : base(
            HocrNodeType.ContentArea,
            id,
            parentId,
            title,
            language,
            direction,
            children
        )
        {
        }
    }

    public record HocrParagraph : HocrNode
    {
        public HocrParagraph(
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            IEnumerable<IHocrNode> children
        ) : base(
            HocrNodeType.Paragraph,
            id,
            parentId,
            title,
            language,
            direction,
            children
        )
        {
        }
    }

    public record HocrLine : HocrNode
    {
        public HocrLine(
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            IEnumerable<IHocrNode> children
        ) : base(
            HocrNodeType.Line,
            id,
            parentId,
            title,
            language,
            direction,
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
        public HocrTextFloat(
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            IEnumerable<IHocrNode> children
        ) : base(
            id,
            parentId,
            title,
            language,
            direction,
            children
        )
        {
            NodeType = HocrNodeType.TextFloat;
        }
    }

    public record HocrCaption : HocrLine
    {
        public HocrCaption(
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            IEnumerable<IHocrNode> children
        ) : base(
            id,
            parentId,
            title,
            language,
            direction,
            children
        )
        {
            NodeType = HocrNodeType.Caption;
        }
    }

    public sealed record HocrWord : HocrNode
    {
        public HocrWord(
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            string innerText
        ) : base(
            HocrNodeType.Word,
            id,
            parentId,
            title,
            language,
            direction,
            Enumerable.Empty<IHocrNode>()
        )
        {
            InnerText = HtmlEntity.DeEntitize(innerText.Trim().TrimEnd('\u200f'));
            Confidence = int.Parse(GetAttributeFromTitle("x_wconf"));

            Language = language;

            var fsize = GetAttributeFromTitle("x_fsize");
            if (!string.IsNullOrEmpty(fsize))
            {
                FontSize = int.Parse(fsize);
            }
        }

        public string InnerText { get; set; }
        public float Confidence { get; }
        public int FontSize { get; }
    }

    public record HocrImage : HocrNode
    {
        public HocrImage(int id, int parentId, string title, string language, Direction direction) : base(
            HocrNodeType.Image,
            id,
            parentId,
            title,
            language,
            direction,
            Enumerable.Empty<IHocrNode>()
        )
        {
        }
    }
}
