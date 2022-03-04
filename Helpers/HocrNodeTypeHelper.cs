using System;
using System.Collections;
using System.Collections.Generic;
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
        HocrNodeType.Header => HocrNodeType.Paragraph,
        HocrNodeType.Footer => HocrNodeType.Paragraph,
        HocrNodeType.TextFloat => HocrNodeType.ContentArea,
        HocrNodeType.Caption => HocrNodeType.ContentArea,
        HocrNodeType.Word => HocrNodeType.Line,
        HocrNodeType.Image => HocrNodeType.Page,
        _ => throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, null)
    };

    public static IEnumerable<HocrNodeType> GetParentNodeTypes(HocrNodeType nodeType)
    {
        HocrNodeType? iter = nodeType;

        while (iter != HocrNodeType.Page)
        {
            yield return iter!.Value;

            iter = GetParentNodeType(iter.Value);
        }
    }

    public static bool CanNodeTypeBeChildOf(HocrNodeType childType, HocrNodeType parentType) => (child: childType, parent: parentType) switch
    {
        (HocrNodeType.ContentArea, HocrNodeType.Page) => true,
        (HocrNodeType.Paragraph, HocrNodeType.ContentArea) => true,
        (HocrNodeType.Line, HocrNodeType.Paragraph) => true,
        (HocrNodeType.Header, HocrNodeType.Paragraph) => true,
        (HocrNodeType.Footer, HocrNodeType.Paragraph) => true,
        (HocrNodeType.TextFloat, HocrNodeType.ContentArea) => true,
        (HocrNodeType.Caption, HocrNodeType.ContentArea) => true,
        (HocrNodeType.Word, HocrNodeType.Line) => true,
        (HocrNodeType.Word, HocrNodeType.TextFloat) => true,
        (HocrNodeType.Image, HocrNodeType.Page) => true,
        _ => false
    };

    public static string GetIcon(HocrNodeType nodeType) => nodeType switch
    {
        HocrNodeType.Page => "/Icons/document.png",
        HocrNodeType.ContentArea => "/Icons/layers-group.png",
        HocrNodeType.Paragraph => "/Icons/edit-pilcrow.png",
        HocrNodeType.Line => "/Icons/edit-lipsum.png",
        HocrNodeType.TextFloat => "/Icons/edit-indent.png",
        HocrNodeType.Caption => "/Icons/edit-rule.png",
        HocrNodeType.Footer => "/Icons/document-hf-select-footer.png",
        HocrNodeType.Header => "/Icons/edit-heading.png",
        HocrNodeType.Image => "/Icons/image.png",
        HocrNodeType.Word => "/Icons/edit-quotation.png",
        _ => throw new ArgumentOutOfRangeException()
    };
}
