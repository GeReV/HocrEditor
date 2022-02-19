using System;
using System.Collections.ObjectModel;
using System.Linq;
using HocrEditor.Helpers;
using HocrEditor.Models;

namespace HocrEditor.ViewModels;

public class HocrNodeTypeViewModel
{
    public static readonly ReadOnlyCollection<HocrNodeTypeViewModel> NodeTypes = new[]
        {
            HocrNodeType.Page,
            HocrNodeType.ContentArea,
            HocrNodeType.Paragraph,
            HocrNodeType.Line,
            HocrNodeType.Header,
            HocrNodeType.Footer,
            HocrNodeType.Caption,
            HocrNodeType.TextFloat,
            HocrNodeType.Word,
            HocrNodeType.Image
        }
        .Select(type => new HocrNodeTypeViewModel(type))
        .ToList()
        .AsReadOnly();

    private HocrNodeTypeViewModel(HocrNodeType nodeType)
    {
        NodeType = nodeType;
    }

    public HocrNodeType NodeType { get; }

    public string? IconPath => HocrNodeTypeHelper.GetIcon(NodeType);

    public string? ToolTip => Enum.GetName(NodeType);
}
