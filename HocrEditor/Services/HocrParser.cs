﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HocrEditor.Helpers;
using HocrEditor.Models;
using HtmlAgilityPack;

namespace HocrEditor.Services
{
    public class HocrParser
    {
        private int idCounter;

        public HocrDocument Parse(string filename)
        {
            var stream = File.OpenRead(filename);

            var doc = new HtmlDocument();
            doc.Load(stream);

            var hocrDocument = Parse(doc);

            foreach (var page in hocrDocument.Pages)
            {
                page.ImageFilename = Path.Combine(Path.GetDirectoryName(filename) ?? string.Empty, page.ImageFilename);
            }

            return hocrDocument;
        }

        public HocrDocument Parse(HtmlDocument document)
        {
            var pageNodes = document.DocumentNode.SelectNodes("//div[@class='ocr_page']");

            var pages = new List<HocrPage>();

            foreach (var pageNode in pageNodes)
            {
                idCounter = 0;

                var page = (HocrPage)Parse(pageNode, -1, string.Empty, Direction.Ltr);

                // Try to guess page direction based on the direction counts.
                var pageDirection = page.Descendants
                    .CountBy(n => n.Direction)
                    .MaxBy(pair => pair.Value)
                    .Key;

                page.Direction = pageDirection;

                pages.Add(page);
            }

            var hocrDocument = new HocrDocument(pages);
            //
            // var capabilities = document.DocumentNode.SelectSingleNode("//head/meta[@name='ocr-capabilities']")
            //     .GetAttributeValue("content", string.Empty)
            //     .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            //
            // hocrDocument.Capabilities.AddRange(capabilities);

            return hocrDocument;
        }

        private HocrNode Parse(HtmlNode node, int parentId, string language, Direction direction)
        {
            var nodeId = idCounter++;

            language = node.GetAttributeValue("lang", language);

            direction = node.GetAttributeValue("dir", string.Empty) switch
            {
                "rtl" => Direction.Rtl,
                "ltr" => Direction.Ltr,
                _ => direction,
            };

            var children = node.ChildNodes
                .Where(childNode => !string.Equals(childNode.Name, "#text", StringComparison.Ordinal) && !childNode.HasClass("ocr_separator"))
                .Select(childNode => Parse(childNode, nodeId, language, direction))
                .ToList();

            return HocrNode.FromHtmlNode(node, nodeId, parentId, language, direction, children);
        }
    }
}
