using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HocrEditor.Services;

namespace HocrEditor.ViewModels
{
    public class HocrNodeViewModel : ViewModelBase
    {
        public IHocrNode HocrNode { get; }

        public string Id { get; }
        public string? ParentId { get; set; }

        public bool IsRoot { get; set; }

        public HocrNodeType NodeType => HocrNode.NodeType;

        public string InnerText => NodeType switch
        {
            HocrNodeType.Word or HocrNodeType.Line => HocrNode.InnerText,
            _ => Enum.GetName(HocrNode.NodeType) ?? string.Empty
        };

        public Rect BBox { get; set; }

        public HocrNodeViewModel? Parent { get; set; }

        public ObservableCollection<HocrNodeViewModel> Children { get; set; } = new();

        public bool IsExpanded { get; set; }

        public bool IsSelected { get; set; }

        public HocrNodeViewModel(IHocrNode node)
        {
            HocrNode = node;

            Id = node.Id;
            ParentId = string.IsNullOrEmpty(node.ParentId) ? null : node.ParentId;

            BBox = node.BBox;
        }

        public IEnumerable<HocrNodeViewModel> Descendents => Children.RecursiveSelect(n => n.Children);

        public IEnumerable<HocrNodeViewModel> Ascendants
        {
            get
            {
                var parent = Parent;

                while (parent is { })
                {
                    yield return parent;

                    parent = parent.Parent;
                }
            }
        }
    }
}
