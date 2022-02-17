using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Threading;
using HocrEditor.Helpers;
using HocrEditor.Models;
using PropertyChanged;

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

        public string BuildInnerText() => this switch
        {
            _ when IsLineElement => JoinInnerText(" ", Children),
            _ when NodeType is HocrNodeType.Image => "Image",
            _ when NodeType is HocrNodeType.Word => ((HocrWord)HocrNode).InnerText,
            _ when NodeType is HocrNodeType.Paragraph => JoinInnerText(LineSeparator, Children),
            _ when NodeType is HocrNodeType.Page or HocrNodeType.ContentArea => JoinInnerText(ParagraphSeparator, Children),
            _ => throw new ArgumentOutOfRangeException()
        };

        public HocrNodeViewModel(IHocrNode node)
        {
            HocrNode = node;

            Children.CollectionChanged += ChildrenOnCollectionChanged;
        }

        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global -- Setter used by PropertyChangedCommand.
        public IHocrNode HocrNode { get; set; }

        public int Id
        {
            get => HocrNode.Id;
            set
            {
                HocrNode.Id = value;

                foreach (var child in Children)
                {
                    child.ParentId = value;
                }
            }
        }

        public int ParentId
        {
            get => HocrNode.ParentId;
            set => HocrNode.ParentId = value;
        }

        public HocrNodeType NodeType => HocrNode.NodeType;

        public bool IsLineElement => HocrNode.IsLineElement;

        public bool IsRoot => ParentId < 0;

        public string InnerText
        {
            get => BuildInnerText();
            set
            {
                if (NodeType != HocrNodeType.Word)
                {
                    throw new InvalidOperationException($"Cannot change {nameof(InnerText)} on a non-word node.");
                }

                ((HocrWord)HocrNode).InnerText = value;

                UpdateAscendantsDisplayText();
            }
        }

        public string DisplayText
        {
            get
            {
                var innerText = InnerText;

                var result = innerText.Length > MAX_INNER_TEXT_LENGTH
                    ? innerText.Remove(MAX_INNER_TEXT_LENGTH).TrimEnd() + ELLIPSIS
                    : innerText;

                return result.ReplaceLineEndings(" ");
            }
            set
            {
                if (IsEditable)
                {
                    InnerText = value;
                }
                else
                {
                    throw new InvalidOperationException($"Cannot edit {nameof(DisplayText)} for this node");
                }
            }
        }

        public Rect BBox
        {
            get => HocrNode.BBox;
            set => HocrNode.BBox = value;
        }

        public HocrNodeViewModel? Parent
        {
            get => parent;
            set
            {
                parent = value;

                ParentId = parent?.Id ?? -1;
            }
        }

        public ObservableCollection<HocrNodeViewModel> Children { get; } = new();

        [DoNotSetChanged]
        public bool IsExpanded { get; set; }


        private bool isSelected;

        [DoNotSetChanged]
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                isSelected = value;

                if (value)
                {
                    // TODO: Is this a correct usage?
                    Dispatcher.CurrentDispatcher.BeginInvoke(
                        () =>
                        {
                            foreach (var node in Ascendants)
                            {
                                // Close on deselect?
                                node.IsExpanded = true;
                            }
                        },
                        DispatcherPriority.ContextIdle
                    );
                }
            }
        }

        public IEnumerable<HocrNodeViewModel> Descendants => Children.RecursiveSelect(n => n.Children);

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
            HocrNodeType.Word => null,
            _ => HocrNodeTypeHelper.GetIcon(NodeType)
        };

        public string? IconTooltip => Enum.GetName(NodeType);

        public bool IsEditable => NodeType == HocrNodeType.Word;

        [DoNotSetChanged]
        public bool IsEditing { get; set; }

        private void ChildrenOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateAscendantsDisplayText();
        }

        private void UpdateAscendantsDisplayText()
        {
            foreach (var item in Ascendants.Prepend(this))
            {
                item.OnPropertyChanged(nameof(InnerText));
                item.OnPropertyChanged(nameof(DisplayText));
            }
        }

        public override void Dispose()
        {
            Children.CollectionChanged -= ChildrenOnCollectionChanged;
        }
    }
}
