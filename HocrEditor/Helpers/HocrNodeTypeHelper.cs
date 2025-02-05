﻿using System;
using System.Collections;
using System.Collections.Generic;
using HocrEditor.Models;

namespace HocrEditor.Helpers;

public static class HocrNodeTypeHelper
{
    public static bool IsLineElement(HocrNodeType nodeType) => nodeType is
        HocrNodeType.Line or
        HocrNodeType.Header or
        HocrNodeType.Footer or
        HocrNodeType.Caption or
        HocrNodeType.TextFloat;

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
        _ => throw new ArgumentOutOfRangeException(nameof(nodeType), nodeType, message: null),
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
        (HocrNodeType.ContentArea, HocrNodeType.Page) or
        (HocrNodeType.Paragraph, HocrNodeType.ContentArea) or
        (HocrNodeType.Line, HocrNodeType.Paragraph) or
        (HocrNodeType.Header, HocrNodeType.Paragraph) or
        (HocrNodeType.Footer, HocrNodeType.Paragraph) or
        (HocrNodeType.TextFloat, HocrNodeType.Paragraph) or
        (HocrNodeType.Caption, HocrNodeType.Paragraph) or
        (HocrNodeType.Word, HocrNodeType.Line) or
        (HocrNodeType.Word, HocrNodeType.TextFloat) or
        (HocrNodeType.Word, HocrNodeType.Caption) or
        (HocrNodeType.Word, HocrNodeType.Header) or
        (HocrNodeType.Word, HocrNodeType.Footer) or
        (HocrNodeType.Image, HocrNodeType.Page) => true,
        _ => false,
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
        _ => throw new ArgumentOutOfRangeException(nameof(nodeType)),
    };

    public static string HocrClassNameForType(HocrNodeType nodeType) =>
        nodeType switch
        {
            HocrNodeType.Page => "ocr_page",
            HocrNodeType.ContentArea => "ocr_carea",
            HocrNodeType.Paragraph => "ocr_par",
            HocrNodeType.Line => "ocr_line",
            HocrNodeType.Header => "ocr_header",
            HocrNodeType.Footer => "ocr_footer",
            HocrNodeType.TextFloat => "ocr_textfloat",
            HocrNodeType.Caption => "ocr_caption",
            HocrNodeType.Word => "ocrx_word",
            HocrNodeType.Image => "ocr_image",
            _ => throw new ArgumentOutOfRangeException(nameof(nodeType)),
        };

    public static string HocrIdForType(HocrNodeType nodeType) =>
        nodeType switch
        {
            _ when IsLineElement(nodeType) => "line",
            HocrNodeType.Page => "page",
            HocrNodeType.ContentArea => "block",
            HocrNodeType.Paragraph => "par",
            HocrNodeType.Word => "word",
            HocrNodeType.Image => "image",
            _ => throw new ArgumentOutOfRangeException(nameof(nodeType)),
        };

    public static string ElementTypeForType(HocrNodeType nodeType) =>
        nodeType switch
        {
            HocrNodeType.Page or HocrNodeType.ContentArea => "div",
            HocrNodeType.Paragraph => "p",
            HocrNodeType.Word => "span",
            HocrNodeType.Image => "div",
            _ when IsLineElement(nodeType) => "span",
            _ => throw new ArgumentOutOfRangeException(nameof(nodeType)),
        };
}
