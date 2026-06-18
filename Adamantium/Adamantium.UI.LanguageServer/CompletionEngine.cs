namespace Adamantium.UI.LanguageServer;

public enum AumlCompletionItemKind { Element, Property, Value, Directive }

public sealed record AumlCompletionItem(string Label, AumlCompletionItemKind Kind, string? Detail = null);

/// <summary>
/// Produces AUML completions at a caret position: element names, an element's settable
/// properties (plus the <c>x:</c> directives), or an attribute's value set (enum members /
/// booleans). Resolves xmlns prefixes from the buffer's declarations.
/// </summary>
public sealed class CompletionEngine
{
    private const string FallbackXmlns = "http://adamantium/ui";

    private readonly AumlTypeModel _model;

    public CompletionEngine(AumlTypeModel model) => _model = model;

    public IReadOnlyList<AumlCompletionItem> Complete(string text, int offset)
    {
        var ctx = AumlCaretContext.Detect(text, offset);
        var namespaces = AumlNamespaces.Scan(text);
        return ctx.Kind switch
        {
            AumlCompletionKind.ElementName => CompleteElements(ctx, namespaces),
            AumlCompletionKind.AttributeName => CompleteAttributes(ctx, namespaces),
            AumlCompletionKind.AttributeValue => CompleteValues(ctx, namespaces),
            AumlCompletionKind.MarkupExtensionName => CompleteMarkupExtensionName(ctx),
            AumlCompletionKind.MarkupExtensionArg => CompleteMarkupExtensionArg(ctx, namespaces, text, offset),
            _ => Array.Empty<AumlCompletionItem>()
        };
    }

    // After '{': offer the available markup-extension names (TemplateBinding, ResourceReference, …).
    private IReadOnlyList<AumlCompletionItem> CompleteMarkupExtensionName(AumlCompletionContext ctx) =>
        _model.GetMarkupExtensions()
            .Where(n => Matches(n, ctx.Prefix))
            .Select(n => new AumlCompletionItem(n, AumlCompletionItemKind.Element))
            .ToList();

    // Inside "{Name arg}": complete the argument. v1 handles {TemplateBinding <prop>} -> the templated parent's
    // (enclosing ControlTemplate's TargetType) settable properties, which is the common case.
    private IReadOnlyList<AumlCompletionItem> CompleteMarkupExtensionArg(
        AumlCompletionContext ctx, IReadOnlyDictionary<string, string> namespaces, string text, int offset)
    {
        if (ctx.MarkupExtension is "TemplateBinding" or "TemplateBindingExtension")
        {
            var target = FindControlTemplateTargetType(text, offset, namespaces);
            if (target is null) return Array.Empty<AumlCompletionItem>();
            return _model.GetProperties(target)
                .Where(p => Matches(p.Name, ctx.Prefix))
                .OrderBy(p => p.Name)
                .Select(p => new AumlCompletionItem(p.Name, AumlCompletionItemKind.Property, p.Type?.Name))
                .ToList();
        }
        return Array.Empty<AumlCompletionItem>();
    }

    /// <summary>The TargetType of the ControlTemplate enclosing the caret — the nearest <c>TargetType="..."</c>
    /// before the caret (pragmatic scan; good enough for completing a TemplateBinding inside a template).</summary>
    private Adamantium.UI.Markup.CodeGeneration.IResolvedType? FindControlTemplateTargetType(
        string text, int offset, IReadOnlyDictionary<string, string> namespaces)
    {
        int region = Math.Min(offset, text.Length);
        int idx = text.LastIndexOf("TargetType", Math.Max(0, region - 1), StringComparison.Ordinal);
        if (idx < 0) return null;
        int q1 = text.IndexOf('"', idx);
        if (q1 < 0 || q1 >= region) return null;
        int q2 = text.IndexOf('"', q1 + 1);
        if (q2 < 0) return null;
        var (prefix, local) = SplitName(text.Substring(q1 + 1, q2 - q1 - 1).Trim());
        return ResolveType(prefix, local, namespaces);
    }

    private IReadOnlyList<AumlCompletionItem> CompleteElements(AumlCompletionContext ctx, IReadOnlyDictionary<string, string> namespaces)
    {
        var (prefix, partial) = SplitName(ctx.Prefix);

        // Property-element syntax: <Owner.Property> — complete the owner type's settable properties.
        int dot = partial.IndexOf('.');
        if (dot >= 0)
        {
            var owner = ResolveType(prefix, partial[..dot], namespaces);
            if (owner is null) return Array.Empty<AumlCompletionItem>();
            var memberPartial = partial[(dot + 1)..];
            var ownerDot = partial[..(dot + 1)];   // "Owner." — kept so the inserted local name stays whole
            return _model.GetProperties(owner, includeReadOnlyCollections: true)
                .Where(p => Matches(p.Name, memberPartial))
                .OrderBy(p => p.Name)
                .Select(p => new AumlCompletionItem(ownerDot + p.Name, AumlCompletionItemKind.Property, p.Type?.Name))
                .ToList();
        }

        var xmlns = ResolveXmlns(prefix, namespaces);
        if (xmlns.Length == 0) return Array.Empty<AumlCompletionItem>();

        return _model.GetElements(xmlns)
            .Where(t => Matches(t.Name, partial))
            .OrderBy(t => t.Name)
            .Select(t => new AumlCompletionItem(t.Name, AumlCompletionItemKind.Element))
            .ToList();
    }

    private IReadOnlyList<AumlCompletionItem> CompleteAttributes(AumlCompletionContext ctx, IReadOnlyDictionary<string, string> namespaces)
    {
        var (attrPrefix, partial) = SplitName(ctx.Prefix);

        // Attached-property syntax: Owner.Property="..." — complete the owner type's attached properties.
        int dot = partial.IndexOf('.');
        if (dot >= 0)
        {
            var owner = ResolveType(attrPrefix, partial[..dot], namespaces);
            if (owner is null) return Array.Empty<AumlCompletionItem>();
            var memberPartial = partial[(dot + 1)..];
            var ownerDot = partial[..(dot + 1)];   // "Owner."
            return _model.GetAttachedProperties(owner)
                .Where(p => Matches(p.Name, memberPartial))
                .OrderBy(p => p.Name)
                .Select(p => new AumlCompletionItem(ownerDot + p.Name, AumlCompletionItemKind.Property, p.Type?.Name))
                .ToList();
        }

        // Typing "x:..." (an attribute in the directives namespace) -> offer x: directives only.
        if (attrPrefix.Length > 0 && namespaces.TryGetValue(attrPrefix, out var ns) && ns == AumlXDirectives.Xmlns)
            return AumlXDirectives.All
                .Where(d => Matches(d.Name, partial))
                .Select(d => new AumlCompletionItem(d.Name, AumlCompletionItemKind.Directive, d.Detail))
                .ToList();

        var items = new List<AumlCompletionItem>();

        var element = ResolveElement(ctx.ElementName, namespaces);
        if (element is not null)
            items.AddRange(_model.GetProperties(element)
                .Where(p => Matches(p.Name, ctx.Prefix))
                .OrderBy(p => p.Name)
                .Select(p => new AumlCompletionItem(p.Name, AumlCompletionItemKind.Property, p.Type?.Name)));

        // Also surface the x: directives (prefixed) when no prefix is being typed yet.
        var directivePrefix = namespaces.FirstOrDefault(kv => kv.Value == AumlXDirectives.Xmlns).Key;
        if (attrPrefix.Length == 0 && directivePrefix is { Length: > 0 })
            foreach (var d in AumlXDirectives.All)
            {
                var label = $"{directivePrefix}:{d.Name}";
                if (Matches(label, ctx.Prefix))
                    items.Add(new AumlCompletionItem(label, AumlCompletionItemKind.Directive, d.Detail));
            }

        return items;
    }

    private IReadOnlyList<AumlCompletionItem> CompleteValues(AumlCompletionContext ctx, IReadOnlyDictionary<string, string> namespaces)
    {
        var element = ResolveElement(ctx.ElementName, namespaces);
        var propertyType = element is null ? null : _model.GetPropertyType(element, ctx.AttributeName ?? "");
        if (propertyType is null) return Array.Empty<AumlCompletionItem>();

        return _model.GetValueCompletions(propertyType)
            .Where(v => Matches(v, ctx.Prefix))
            .Select(v => new AumlCompletionItem(v, AumlCompletionItemKind.Value, propertyType.Name))
            .ToList();
    }

    private Adamantium.UI.Markup.CodeGeneration.IResolvedType? ResolveElement(string? qualifiedName, IReadOnlyDictionary<string, string> namespaces)
    {
        var (prefix, local) = SplitName(qualifiedName ?? "");
        var xmlns = ResolveXmlns(prefix, namespaces);
        return xmlns.Length == 0 ? null : _model.GetElement(xmlns, local);
    }

    /// <summary>Resolves a (possibly xmlns-prefixed) type name; an unprefixed name may live in any registered namespace.</summary>
    private Adamantium.UI.Markup.CodeGeneration.IResolvedType? ResolveType(string xmlnsPrefix, string typeName, IReadOnlyDictionary<string, string> namespaces)
    {
        var xmlns = ResolveXmlns(xmlnsPrefix, namespaces);
        if (xmlns.Length > 0 && _model.GetElement(xmlns, typeName) is { } resolved) return resolved;
        return xmlnsPrefix.Length == 0 ? _model.FindElement(typeName) : null;
    }

    private static string ResolveXmlns(string prefix, IReadOnlyDictionary<string, string> namespaces)
    {
        if (namespaces.TryGetValue(prefix, out var uri)) return uri;
        return prefix.Length == 0 ? FallbackXmlns : "";   // unknown prefix -> no namespace
    }

    private static (string Prefix, string Local) SplitName(string name)
    {
        int colon = name.IndexOf(':');
        return colon < 0 ? ("", name) : (name[..colon], name[(colon + 1)..]);
    }

    private static bool Matches(string candidate, string prefix) =>
        string.IsNullOrEmpty(prefix) || candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
}
