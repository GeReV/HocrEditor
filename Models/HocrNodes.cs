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
            IEnumerable<HocrNode> children
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

        public string Image { get; set; }

        public IEnumerable<HocrNode> Descendants => ChildNodes.RecursiveSelect(n => n.ChildNodes);
    }

    public record HocrContentArea : HocrNode
    {
        public HocrContentArea(
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            IEnumerable<HocrNode> children
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
            IEnumerable<HocrNode> children
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
            IEnumerable<HocrNode> children
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
            var size = GetAttributeFromTitle("x_size");
            if (string.IsNullOrEmpty(size))
            {
                size = GetAttributeFromTitle("x_fsize");
            }

            if (!string.IsNullOrEmpty(size))
            {
                FontSize = (int)float.Parse(size);
            }

            var baseline = GetAttributeFromTitle("baseline")
                .Split(" ", StringSplitOptions.RemoveEmptyEntries)
                .Select(float.Parse)
                .ToArray();

            if (baseline.Length == 2)
            {
                Baseline = (baseline[0], (int)baseline[1]);
            }
        }

        public (float,int) Baseline { get; }
        public int FontSize { get; }
    }

    public record HocrTextFloat : HocrLine
    {
        public HocrTextFloat(
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            IEnumerable<HocrNode> children
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
            IEnumerable<HocrNode> children
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

    public record HocrHeader : HocrLine
    {
        public HocrHeader(
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            IEnumerable<HocrNode> children
        ) : base(
            id,
            parentId,
            title,
            language,
            direction,
            children
        )
        {
            NodeType = HocrNodeType.Header;
        }
    }

    public record HocrFooter : HocrLine
    {
        public HocrFooter(
            int id,
            int parentId,
            string title,
            string language,
            Direction direction,
            IEnumerable<HocrNode> children
        ) : base(
            id,
            parentId,
            title,
            language,
            direction,
            children
        )
        {
            NodeType = HocrNodeType.Footer;
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
            Enumerable.Empty<HocrNode>()
        )
        {
            InnerText = HtmlEntity.DeEntitize(innerText.Trim().TrimEnd('\u200f'));

            var confidence = GetAttributeFromTitle("x_wconf");

            if (!string.IsNullOrEmpty(confidence))
            {
                Confidence = int.Parse(confidence);
            }
        }

        public string InnerText { get; set; }
        public float Confidence { get; }
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
            Enumerable.Empty<HocrNode>()
        )
        {
        }
    }
}
