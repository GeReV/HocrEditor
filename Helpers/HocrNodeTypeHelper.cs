using System;
using HocrEditor.Models;

namespace HocrEditor.Helpers;

public static class HocrNodeTypeHelper
{
    public static HocrNodeType? GetParentNodeType(HocrNodeType nodeType) => nodeType switch
    {
        HocrNodeType.Page => null,
        HocrNodeType.ContentArea => HocrNodeType.Page,
        HocrNodeType.Paragraph => HocrNodeType.ContentArea,
        HocrNodeType.Line => HocrNodeType.Paragraph,
        HocrNodeType.TextFloat => HocrNodeType.ContentArea,
        HocrNodeType.Caption => HocrNodeType.ContentArea,
        HocrNodeType.Word => HocrNodeType.Line,
        HocrNodeType.Image => HocrNodeType.Page,
        _ => throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, null)
    };
}
