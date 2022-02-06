using System.Collections.Generic;
using HtmlAgilityPack;

namespace HocrEditor.Models
{
    public interface IHocrNode
    {
        public HocrNodeType NodeType { get; }
        public string Title { get; }
        public int Id { get; set; }
        public int ParentId { get; set; }
        Direction Direction { get; }
        string Language { get; }
        public Rect BBox { get; set; }
        public List<IHocrNode> ChildNodes { get; }
        public bool IsLineElement { get; }
    }
}
