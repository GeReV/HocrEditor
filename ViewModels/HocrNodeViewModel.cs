using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using HocrEditor.Helpers;
using HocrEditor.Models;

namespace HocrEditor.ViewModels
{
    public class HocrNodeViewModel : ViewModelBase
    {
        private const int MAX_INNER_TEXT_LENGTH = 15;
        private const char ELLIPSIS = '…';
        private static readonly string LineSeparator = Environment.NewLine;
        private static readonly string ParagraphSeparator = Environment.NewLine + Environment.NewLine;

        private HocrNodeViewModel? parent;

        private static string JoinInnerText(string separator, IEnumerable<HocrNodeViewModel> nodes) =>
            string.Join(separator, nodes.Select(n => n.InnerText));

        public string BuildInnerText() => NodeType switch
        {
            HocrNodeType.Image => "Image",
            HocrNodeType.Word => ((HocrWord)HocrNode).InnerText,
            HocrNodeType.Line or HocrNodeType.Caption or HocrNodeType.TextFloat => JoinInnerText(" ", Children),
            HocrNodeType.Paragraph => JoinInnerText(LineSeparator, Children),
            HocrNodeType.Page or HocrNodeType.ContentArea => JoinInnerText(ParagraphSeparator, Children),
            _ => throw new ArgumentOutOfRangeException()
        };

        public HocrNodeViewModel(IHocrNode node)
        {
            HocrNode = node;

            Id = node.Id;
            NodeType = node.NodeType;

            BBox = node.BBox;

            Children.CollectionChanged += ChildrenOnCollectionChanged;
        }

        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global -- Setter used by PropertyChangedCommand.
        public IHocrNode HocrNode { get; }

        public string Id { get; }

        public string? ParentId { get; set; }

        public bool IsRoot { get; set; }
        public HocrNodeType NodeType { get; set; }

        public string InnerText => BuildInnerText();

        public string DisplayText
        {
            get
            {
                var result = InnerText.Length > MAX_INNER_TEXT_LENGTH
                    ? InnerText.Remove(MAX_INNER_TEXT_LENGTH).TrimEnd() + ELLIPSIS
                    : InnerText;

                return result.ReplaceLineEndings(" ");
            }
        }

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

        public ObservableCollection<HocrNodeViewModel> Children { get; } = new();

        public bool IsExpanded { get; set; }

        public bool IsSelected { get; set; }

        public IEnumerable<HocrNodeViewModel> Descendents => Children.RecursiveSelect(n => n.Children);

        public IEnumerable<HocrNodeViewModel> Ascendants
        {
            get
            {
                var item = Parent;

                while (item is { })
                {
                    yield return item;

                    item = item.Parent;
                }
            }
        }

        public string? IconPath => NodeType switch
        {
            HocrNodeType.Page => "/Icons/file-lines.png",
            HocrNodeType.ContentArea => "/Icons/object-group.png",
            HocrNodeType.Paragraph => "/Icons/paragraph.png",
            HocrNodeType.Line or HocrNodeType.TextFloat or HocrNodeType.Caption => "/Icons/i-cursor.png",
            HocrNodeType.Image => "/Icons/image.png",
            HocrNodeType.Word => null,
            _ => throw new ArgumentOutOfRangeException()
        };

        public string? IconTooltip => Enum.GetName(NodeType);

        private void ChildrenOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            foreach (var item in Ascendants.Prepend(this))
            {
                item.OnPropertyChanged(nameof(InnerText));
                item.OnPropertyChanged(nameof(DisplayText));
            }
        }
    }
}
