using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.LanguageServer;

/// <summary>One node in the document outline: an element, its optional x:Name, and children.</summary>
public sealed record AumlSymbol(string Name, string Detail, int Line, int Character, int Length, IReadOnlyList<AumlSymbol> Children);

/// <summary>
/// Builds the document outline (Structure view / breadcrumbs / go-to-symbol) from a well-formed
/// AUML document: each element becomes a symbol, nested under its parent, with its x:Name shown.
/// </summary>
public static class DocumentSymbolEngine
{
    public static IReadOnlyList<AumlSymbol> Symbols(string text)
    {
        AumlDocument document;
        try { document = AumlParser.Parse(text); }
        catch { return Array.Empty<AumlSymbol>(); }
        if (document.HasErrors || document.Root is not AumlAstObjectNode root)
            return Array.Empty<AumlSymbol>();

        return new[] { ToSymbol(root) };
    }

    private static AumlSymbol ToSymbol(AumlAstObjectNode node)
    {
        var name = node.TypeReference.Name;
        var xName = XName(node);

        var children = new List<AumlSymbol>();
        // Property-element values (e.g. <Setter.Value><ControlTemplate>…) and direct children.
        foreach (var property in node.GetProperties())
            foreach (var value in property.Values)
                if (value is AumlAstObjectNode nested)
                    children.Add(ToSymbol(nested));
        foreach (var child in node.GetLogicalChildrenObjects())
            children.Add(ToSymbol(child));

        return new AumlSymbol(
            name,
            xName is null ? "" : $"#{xName}",
            Math.Max(0, node.Line - 1),
            Math.Max(0, node.Position - 1),
            Math.Max(1, name.Length),
            children);
    }

    private static string? XName(AumlAstObjectNode node) =>
        node.Children.OfType<AumlAstDirective>().FirstOrDefault(d => d.Name == "Name")?.Value is AumlAstTextNode text
            ? text.Text
            : null;
}
