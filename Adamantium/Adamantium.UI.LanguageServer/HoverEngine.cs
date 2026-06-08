using Adamantium.UI.Markup.CodeGeneration;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// Produces hover text for the token under the caret: an element resolves to its C# type, an
/// attribute to its property type (or an <c>x:</c> directive description). Reuses the caret-context
/// detector by placing the caret at the end of the hovered token.
/// </summary>
public sealed class HoverEngine
{
    private const string FallbackXmlns = "http://adamantium/ui";

    private readonly AumlTypeModel _model;

    public HoverEngine(AumlTypeModel model) => _model = model;

    public string? Hover(string text, int offset)
    {
        if (string.IsNullOrEmpty(text) || offset < 0 || offset > text.Length) return null;

        int tokenEnd = offset;
        while (tokenEnd < text.Length && IsNameChar(text[tokenEnd])) tokenEnd++;

        var namespaces = AumlNamespaces.Scan(text);
        var ctx = AumlCaretContext.Detect(text, tokenEnd);
        return ctx.Kind switch
        {
            AumlCompletionKind.ElementName => HoverElement(ctx.Prefix, namespaces),
            AumlCompletionKind.AttributeName => HoverAttribute(ctx, namespaces),
            _ => null
        };
    }

    private string? HoverElement(string qualifiedName, IReadOnlyDictionary<string, string> namespaces)
    {
        var element = ResolveElement(qualifiedName, namespaces);
        if (element is null) return null;

        var hover = $"**{element.Name}** — element\n\n`{element.FullName}`";
        if (element.BaseType is { } baseType)
            hover += $"\n\nInherits `{baseType.FullName}`";
        return hover;
    }

    private string? HoverAttribute(AumlCompletionContext ctx, IReadOnlyDictionary<string, string> namespaces)
    {
        var (attrPrefix, local) = SplitName(ctx.Prefix);

        // x: directive?
        if (attrPrefix.Length > 0 && namespaces.TryGetValue(attrPrefix, out var ns) && ns == AumlXDirectives.Xmlns)
        {
            var directive = AumlXDirectives.All.FirstOrDefault(d => d.Name == local);
            return directive.Name is null ? null : $"**{attrPrefix}:{directive.Name}** — directive\n\n{directive.Detail}";
        }

        var element = ResolveElement(ctx.ElementName, namespaces);
        if (element is null) return null;

        var type = _model.GetPropertyType(element, local);
        if (type is not null)
            return $"**{local}** : `{type.Name}`\n\non `{element.FullName}`";

        // Known member that isn't a settable property (event, read-only, …).
        return _model.IsKnownAttribute(element, local) ? $"**{local}**\n\non `{element.FullName}`" : null;
    }

    private IResolvedType? ResolveElement(string? qualifiedName, IReadOnlyDictionary<string, string> namespaces)
    {
        var (prefix, local) = SplitName(qualifiedName ?? "");
        var xmlns = ResolveXmlns(prefix, namespaces);
        return xmlns.Length == 0 ? null : _model.GetElement(xmlns, local);
    }

    private static string ResolveXmlns(string prefix, IReadOnlyDictionary<string, string> namespaces) =>
        namespaces.TryGetValue(prefix, out var uri) ? uri : prefix.Length == 0 ? FallbackXmlns : "";

    private static (string Prefix, string Local) SplitName(string name)
    {
        int colon = name.IndexOf(':');
        return colon < 0 ? ("", name) : (name[..colon], name[(colon + 1)..]);
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or ':';
}
