using System.Collections.Generic;
using System.Collections.ObjectModel;
using HocrEditor.Helpers;
using HocrEditor.Models;

namespace HocrEditor.ViewModels
{
    public class HocrNodeViewModel : ViewModelBase
    {
        private const int MAX_INNER_TEXT_LENGTH = 15;
        private const char ELLIPSIS = '…';

        private HocrNodeViewModel? parent;

        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global -- Setter used by PropertyChangedCommand.
        public IHocrNode HocrNode { get; set; }

        public string Id { get; }

        public string? ParentId { get; set; }

        public bool IsRoot { get; set; }

        public HocrNodeType NodeType => HocrNode.NodeType;

        public string InnerText
        {
            get => HocrNode.InnerText;
            set => HocrNode.InnerText = value;
        }

        public string DisplayText => NodeType switch
        {
            HocrNodeType.Word or HocrNodeType.Line or HocrNodeType.Image => InnerText,
            _ => Children[0].DisplayText.Length > MAX_INNER_TEXT_LENGTH ?
                Children[0].DisplayText.Remove(MAX_INNER_TEXT_LENGTH) + ELLIPSIS :
                Children[0].DisplayText
        };




        public Rect BBox { get; set; }

        public HocrNodeViewModel? Parent
        {
            get => parent;
            set
            {
                parent = value;

                ParentId = parent?.Id;
            }
        }

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
