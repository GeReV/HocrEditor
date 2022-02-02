using System.Collections.Generic;
using HtmlAgilityPack;

namespace HocrEditor.Models
{
    public interface IHocrNode
    {
        public HocrNodeType NodeType { get; init; }
        public string Title { get; set; }
        public int Id { get; set; }
        public int ParentId { get; set; }
        Direction Direction { get; init; }
        string Language { get; init; }
        public Rect BBox { get; set; }
        public List<IHocrNode> ChildNodes { get; }
    }
}
