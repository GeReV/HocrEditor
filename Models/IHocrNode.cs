using System.Collections.Generic;
using HtmlAgilityPack;

namespace HocrEditor.Models
{
    public interface IHocrNode
    {
        public HocrNodeType NodeType { get; init; }
        public string HtmlNodeType { get; init; }
        public string Title { get; init; }
        public string Id { get; init; }
        public string? ParentId { get; init; }
        public Rect BBox { get; init; }
        public string InnerText { get; init; }
        public IList<IHocrNode> ChildNodes { get; init; }
    }
}
