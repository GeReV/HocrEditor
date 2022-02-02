using HtmlAgilityPack;

namespace HocrEditor.Helpers;

public static class HtmlDocumentExtensions
{
    public static HtmlNode CreateMeta(this HtmlDocument document, string name, string content)
    {
        var node = document.CreateElement("meta");

        node.SetAttributeValue("name", name);
        node.SetAttributeValue("content", content);

        return node;
    }
}
