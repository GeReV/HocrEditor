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

    public static string? GetIcon(HocrNodeType nodeType) => nodeType switch
    {
        HocrNodeType.Page => "/Icons/file-lines.png",
        HocrNodeType.ContentArea => "/Icons/object-group.png",
        HocrNodeType.Paragraph => "/Icons/paragraph.png",
        HocrNodeType.Line or HocrNodeType.TextFloat or HocrNodeType.Caption => "/Icons/i-cursor.png",
        HocrNodeType.Image => "/Icons/image.png",
        HocrNodeType.Word => "/Icons/font.png",
        _ => throw new ArgumentOutOfRangeException()
    };
}
