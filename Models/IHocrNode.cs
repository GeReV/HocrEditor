using System.Collections.Generic;
using HtmlAgilityPack;

namespace HocrEditor.Models
{
    public interface IHocrNode
    {
        public HocrNodeType NodeType { get; init; }
        public string Title { get; set; }
        public string Id { get; set; }
        public string? ParentId { get; set; }
        public Rect BBox { get; set; }
        public List<IHocrNode> ChildNodes { get; }
    }
}
