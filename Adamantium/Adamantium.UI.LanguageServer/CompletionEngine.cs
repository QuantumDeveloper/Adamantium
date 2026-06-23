using System.Text.RegularExpressions;
using Adamantium.UI.Markup.AST;

namespace Adamantium.UI.LanguageServer;

public enum AumlCompletionItemKind { Element, Property, Value, Directive }

/// <param name="InsertText">Optional snippet to insert instead of <paramref name="Label"/> (LSP snippet syntax,
/// e.g. <c>Name="$0"</c> to drop the caret between the quotes). Null = insert the label verbatim.</param>
/// <param name="ReplaceBack">When set, the item replaces this many characters immediately before the caret (an
/// explicit edit range) instead of letting the client guess the word boundary. Needed for path segments: after
/// <c>Textures/</c> the client would otherwise filter bare file names against the whole "Textures/" prefix and
/// hide them — here only the segment after the last '/' is the prefix/replacement.</param>
public sealed record AumlCompletionItem(string Label, AumlCompletionItemKind Kind, string? Detail = null, string? InsertText = null, int? ReplaceBack = null);

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

    public IReadOnlyList<AumlCompletionItem> Complete(string text, int offset, string? documentPath = null)
    {
        var ctx = AumlCaretContext.Detect(text, offset);
        var namespaces = AumlNamespaces.Scan(text);
        return ctx.Kind switch
        {
            AumlCompletionKind.ElementName => CompleteElements(ctx, namespaces),
            AumlCompletionKind.AttributeName => CompleteAttributes(ctx, namespaces, text, offset),
            AumlCompletionKind.AttributeValue => CompleteValues(ctx, namespaces, documentPath),
            AumlCompletionKind.MarkupExtensionName => CompleteMarkupExtensionName(ctx),
            AumlCompletionKind.MarkupExtensionArg => CompleteMarkupExtensionArg(ctx, namespaces, text, offset),
            _ => []
        };
    }

    // After '{': offer the available markup-extension names (TemplateBinding, ResourceReference, …). ReplaceBack only
    // covers the partial name typed after '{' (so the '{' is preserved, not eaten by the client's word guess), and the
    // snippet closes the brace + drops the caret where the argument goes: "{Binding |}".
    private IReadOnlyList<AumlCompletionItem> CompleteMarkupExtensionName(AumlCompletionContext ctx) =>
        _model.GetMarkupExtensions()
            .Where(n => Matches(n, ctx.Prefix))
            .Select(n => new AumlCompletionItem(n, AumlCompletionItemKind.Element, InsertText: n + " $0}", ReplaceBack: ctx.Prefix.Length))
            .ToList();

    // Inside "{Name arg}": complete the argument generically for ANY markup extension. The extension's own settable
    // properties drive the NAMED arguments (after the first comma); the value of the positional (default) argument and
    // of each named argument is completed by its TYPE from the appropriate external source - a view-model path for a
    // PropertyPath, type names for a Type, enum members / booleans / brush colors for those. Extensions whose value
    // isn't a plain CLR property on the extension (TemplateBinding -> the templated parent's properties; x:Type -> type
    // names; resources -> resource keys) are dispatched by name. Before, only TemplateBinding/Binding-path/x:Type were
    // special-cased and named arguments (Mode/Converter/FallbackValue/...) had no completion at all.
    private IReadOnlyList<AumlCompletionItem> CompleteMarkupExtensionArg(
        AumlCompletionContext ctx, IReadOnlyDictionary<string, string> namespaces, string text, int offset)
    {
        // The extension may be written with a prefix (e.g. {x:Type ...}); match on its local name.
        var ext = ctx.MarkupExtension ?? "";
        var extLocal = ext.Contains(':') ? ext[(ext.IndexOf(':') + 1)..] : ext;
        var extType = _model.ResolveMarkupExtensionType(extLocal);

        var (segment, hasComma) = CurrentExtensionSegment(text, offset);

        // "Name=value" -> complete the value of that named property by its type.
        int eq = segment.IndexOf('=');
        if (eq >= 0)
        {
            var propName = segment[..eq].Trim();
            var partial = segment[(eq + 1)..].TrimStart();
            var propType = extType is null ? null : _model.GetPropertyType(extType, propName);
            return CompleteExtensionValue(extLocal, propType, partial, text, offset, namespaces);
        }

        var seg = segment.Trim();
        var defaultProp = extType is null ? null : _model.GetDefaultProperty(extType);

        // First (positional) segment: complete the extension's default-argument value (its [DefaultProperty], or a
        // name-dispatched source for extensions whose positional value isn't a CLR property of their own).
        if (!hasComma && (defaultProp is not null || IsPositionalValueExtension(extLocal)))
            return CompleteExtensionValue(extLocal, defaultProp?.PropertyType, seg, text, offset, namespaces);

        // After a comma (or an extension with no positional arg) -> the extension's settable property NAMES.
        if (extType is null) return [];
        return _model.GetProperties(extType)
            .Where(p => Matches(p.Name, seg))
            .OrderBy(p => p.Name)
            .Select(p => new AumlCompletionItem(p.Name, AumlCompletionItemKind.Property, p.Type?.Name,
                InsertText: p.Name + "=", ReplaceBack: seg.Length))
            .ToList();
    }

    // Extensions whose positional argument isn't a CLR property on the extension (so no [DefaultProperty]) but which
    // still complete a positional value, dispatched by name in CompleteExtensionValue.
    private static bool IsPositionalValueExtension(string extLocal) =>
        extLocal is "TemplateBinding" or "TemplateBindingExtension" or "Type" or "TypeExtension"
            or "ResourceReference" or "ResourceReferenceExtension";

    /// <summary>Completes the VALUE of a markup-extension argument from the appropriate external source. Special
    /// extensions are dispatched by name (TemplateBinding -> the ControlTemplate TargetType's properties; x:Type ->
    /// type names; resources -> resource keys); everything else is driven by the property's CLR type: a view-model path
    /// for a PropertyPath, type names for a Type, enum members / booleans / brush colors via the type model.</summary>
    private IReadOnlyList<AumlCompletionItem> CompleteExtensionValue(
        string extLocal, Adamantium.UI.Markup.CodeGeneration.IResolvedType? propType,
        string partial, string text, int offset, IReadOnlyDictionary<string, string> namespaces)
    {
        if (extLocal is "TemplateBinding" or "TemplateBindingExtension")
        {
            var target = FindNearestAttributeType(text, offset, "TargetType", namespaces);
            if (target is null) return [];
            return _model.GetProperties(target)
                .Where(p => Matches(p.Name, partial))
                .OrderBy(p => p.Name)
                .Select(p => new AumlCompletionItem(p.Name, AumlCompletionItemKind.Property, p.Type?.Name, ReplaceBack: partial.Length))
                .ToList();
        }

        if (extLocal is "Type" or "TypeExtension")
            return CompleteTypeNames(partial, namespaces);

        // Resource keys (ResourceReference): no resource index in the type model yet -> nothing to offer.
        if (extLocal is "ResourceReference" or "ResourceReferenceExtension")
            return [];

        if (propType is null) return [];

        // PropertyPath value (e.g. Binding.Path) -> view-model bindable path completion.
        if (propType.Name == "PropertyPath")
            return CompleteBindingPath(partial, FindNearestAttributeType(text, offset, "ViewModel", namespaces));

        // A System.Type-valued property -> type names.
        if (propType.Name == "Type")
            return CompleteTypeNames(partial, namespaces);

        // Enum members / booleans / brush colors handled uniformly by the type model.
        var values = _model.GetValueCompletions(propType);
        if (values.Count == 0) return [];
        return values
            .Where(v => Matches(v, partial))
            .Select(v => new AumlCompletionItem(v, AumlCompletionItemKind.Value, propType.Name, ReplaceBack: partial.Length))
            .ToList();
    }

    // The current comma-separated argument segment of a "{Name ...}" body up to the caret (top-level commas only;
    // nested {} are not split for v1), plus whether any comma preceded it (i.e. it isn't the positional first arg).
    private static (string Segment, bool HasComma) CurrentExtensionSegment(string text, int offset)
    {
        int end = Math.Min(offset, text.Length);
        int brace = text.LastIndexOf('{', Math.Max(0, end - 1));
        if (brace < 0) return ("", false);
        var body = text.Substring(brace + 1, end - brace - 1);
        int sp = body.IndexOf(' ');
        var argText = sp < 0 ? "" : body[(sp + 1)..];     // after the extension name
        int comma = argText.LastIndexOf(',');
        return comma >= 0 ? (argText[(comma + 1)..], true) : (argText, false);
    }

    /// <summary>The type named by the nearest <c>&lt;attrLocalName&gt;="..."</c> attribute before the caret
    /// (pragmatic scan): the ControlTemplate's <c>TargetType</c> for a TemplateBinding, or the <c>x:ViewModel</c>
    /// for a Binding. Good enough to scope completion to the enclosing template / data type.</summary>
    private Adamantium.UI.Markup.CodeGeneration.IResolvedType? FindNearestAttributeType(
        string text, int offset, string attrLocalName, IReadOnlyDictionary<string, string> namespaces)
    {
        int region = Math.Min(offset, text.Length);
        var head = text.Substring(0, region);
        // Match the attribute by NAME (optionally xmlns-prefixed) followed by ="...". Anchoring on `name="` is what
        // makes this robust: a plain substring search would match the local name inside a VALUE - e.g. "ViewModel"
        // inside "MainViewModel" - and resolve the wrong text, which is exactly why {Binding} completion went blank.
        var matches = Regex.Matches(head, $@"(?:\w+:)?{Regex.Escape(attrLocalName)}\s*=\s*""([^""]*)""");
        if (matches.Count == 0) return null;
        var value = matches[^1].Groups[1].Value.Trim();   // nearest (last) such attribute before the caret
        var (prefix, local) = SplitName(UnwrapTypeExtension(value));
        return ResolveType(prefix, local, namespaces);
    }

    // Accepts both "prefix:Type" and the x:Type markup-extension form "{x:Type prefix:Type}" (positional, space-
    // separated). Without this, a ViewModel written as {x:Type ...} wouldn't resolve and {Binding} path completion
    // would offer nothing.
    private static string UnwrapTypeExtension(string value)
    {
        if (!value.StartsWith('{') || !value.EndsWith('}')) return value;
        var inner = value[1..^1].Trim();
        var space = inner.IndexOf(' ');
        return space < 0 ? inner : inner[(space + 1)..].Trim();
    }

    /// <summary>Completes a (possibly dotted) binding path against <paramref name="dataType"/>: each segment before
    /// the last narrows to that property's type; the last segment is completed against the narrowed type. Uses
    /// readable properties (get-only included), since bindings read as well as write.</summary>
    private IReadOnlyList<AumlCompletionItem> CompleteBindingPath(
        string path, Adamantium.UI.Markup.CodeGeneration.IResolvedType? dataType)
    {
        if (dataType is null) return [];

        var type = dataType;
        var memberPartial = path;
        int lastDot = path.LastIndexOf('.');
        if (lastDot >= 0)
        {
            memberPartial = path[(lastDot + 1)..];
            foreach (var seg in path[..lastDot].Split('.'))
            {
                var prop = _model.GetBindableProperties(type).FirstOrDefault(p => p.Name == seg);
                if (prop?.Type is null) return [];
                type = prop.Type;
            }
        }

        return _model.GetBindableProperties(type)
            .Where(p => Matches(p.Name, memberPartial))
            .OrderBy(p => p.Name)
            // ReplaceBack covers only the current path segment, so picking an item replaces just "Sh" (not the whole
            // "{Binding Sh"), and the client filters the list by this segment as you type instead of the whole value.
            .Select(p => new AumlCompletionItem(p.Name, AumlCompletionItemKind.Property, p.Type?.Name, ReplaceBack: memberPartial.Length))
            .ToList();
    }

    private IReadOnlyList<AumlCompletionItem> CompleteElements(AumlCompletionContext ctx, IReadOnlyDictionary<string, string> namespaces)
    {
        var (prefix, partial) = SplitName(ctx.Prefix);

        // Property-element syntax: <Owner.Property> — complete the owner type's settable properties.
        int dot = partial.IndexOf('.');
        if (dot >= 0)
        {
            var owner = ResolveType(prefix, partial[..dot], namespaces);
            if (owner is null) return [];
            var memberPartial = partial[(dot + 1)..];
            var ownerDot = partial[..(dot + 1)];   // "Owner." — kept so the inserted local name stays whole
            return _model.GetProperties(owner, includeReadOnlyCollections: true)
                .Where(p => Matches(p.Name, memberPartial))
                .OrderBy(p => p.Name)
                .Select(p => new AumlCompletionItem(ownerDot + p.Name, AumlCompletionItemKind.Property, p.Type?.Name))
                .ToList();
        }

        var xmlns = ResolveXmlns(prefix, namespaces);
        if (xmlns.Length == 0) return [];

        return _model.GetElements(xmlns)
            .Where(t => Matches(t.Name, partial))
            .OrderBy(t => t.Name)
            .Select(t => new AumlCompletionItem(t.Name, AumlCompletionItemKind.Element))
            .ToList();
    }

    private IReadOnlyList<AumlCompletionItem> CompleteAttributes(AumlCompletionContext ctx, IReadOnlyDictionary<string, string> namespaces, string text, int offset)
    {
        var (attrPrefix, partial) = SplitName(ctx.Prefix);

        // Attributes already on this element are hidden so completion can't create a duplicate.
        var used = CollectUsedAttributes(text, offset);

        // Attached-property syntax: Owner.Property="..." — complete the owner type's attached properties.
        int dot = partial.IndexOf('.');
        if (dot >= 0)
        {
            var owner = ResolveType(attrPrefix, partial[..dot], namespaces);
            if (owner is null) return [];
            var memberPartial = partial[(dot + 1)..];
            var ownerDot = partial[..(dot + 1)];   // "Owner."
            return _model.GetAttachedProperties(owner)
                .Where(p => Matches(p.Name, memberPartial) && !used.Contains(ownerDot + p.Name))
                .OrderBy(p => p.Name)
                .Select(p => AttrItem(ownerDot + p.Name, AumlCompletionItemKind.Property, p.Type?.Name))
                .ToList();
        }

        // Typing "x:..." (an attribute in the directives namespace) -> offer x: directives only.
        if (attrPrefix.Length > 0 && namespaces.TryGetValue(attrPrefix, out var ns) && ns == AumlXDirectives.Xmlns)
            return AumlXDirectives.All
                .Where(d => Matches(d.Name, partial) && !used.Contains($"{attrPrefix}:{d.Name}"))
                .Select(d => AttrItem(d.Name, AumlCompletionItemKind.Directive, d.Detail))
                .ToList();

        var items = new List<AumlCompletionItem>();

        var element = ResolveElement(ctx.ElementName, namespaces);
        if (element is not null)
            items.AddRange(_model.GetProperties(element)
                .Where(p => Matches(p.Name, ctx.Prefix) && !used.Contains(p.Name))
                .OrderBy(p => p.Name)
                .Select(p => AttrItem(p.Name, AumlCompletionItemKind.Property, p.Type?.Name)));

        // Also surface the x: directives (prefixed) when no prefix is being typed yet.
        var directivePrefix = namespaces.FirstOrDefault(kv => kv.Value == AumlXDirectives.Xmlns).Key;
        if (attrPrefix.Length == 0 && directivePrefix is { Length: > 0 })
            foreach (var d in AumlXDirectives.All)
            {
                var label = $"{directivePrefix}:{d.Name}";
                if (Matches(label, ctx.Prefix) && !used.Contains(label))
                    items.Add(AttrItem(label, AumlCompletionItemKind.Directive, d.Detail));
            }

        return items;
    }

    /// <summary>
    /// Attribute names already present on the element whose opening tag holds the caret, so completion can exclude
    /// them and never offer a duplicate. Scans from the element's '&lt;' to the tag end (quote-aware) and takes the
    /// identifier before each '=' - covering attributes both before and after the caret.
    /// </summary>
    private static HashSet<string> CollectUsedAttributes(string text, int offset)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        int region = Math.Min(offset, text.Length);
        int lt = text.LastIndexOf('<', Math.Max(0, region - 1));
        if (lt < 0) return used;

        int i = lt + 1;
        bool inQuote = false;
        for (; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"') inQuote = !inQuote;
            else if (!inQuote && (c == '>' || (c == '<' && i > lt + 1))) break;
        }

        var tag = text.Substring(lt, i - lt);
        tag = Regex.Replace(tag, "\"[^\"]*\"", m => new string(' ', m.Length));   // blank quoted values so '=' in a value can't look like an attribute
        foreach (Match m in Regex.Matches(tag, @"([A-Za-z_][\w:.\-]*)\s*="))
            used.Add(m.Groups[1].Value);
        return used;
    }

    private IReadOnlyList<AumlCompletionItem> CompleteValues(AumlCompletionContext ctx, IReadOnlyDictionary<string, string> namespaces, string? documentPath)
    {
        // A type-valued x: directive (x:ViewModel) completes type names directly in its value - the simplified form
        // without {x:Type}. Same type set used inside {x:Type}.
        if (IsTypeReferenceDirective(ctx.AttributeName, namespaces))
            return CompleteTypeNames(ctx.Prefix, namespaces);

        var element = ResolveElement(ctx.ElementName, namespaces);
        var propertyType = element is null ? null : _model.GetPropertyType(element, ctx.AttributeName ?? "");
        if (propertyType is null) return [];

        // Path-typed properties (e.g. Image.Source : ImageSource) take a file path relative to the project root —
        // complete the filesystem instead of enum/bool/brush values.
        if (IsPathLike(propertyType))
            return CompletePaths(ctx.Prefix, documentPath);

        return _model.GetValueCompletions(propertyType)
            .Where(v => Matches(v, ctx.Prefix))
            .Select(v => new AumlCompletionItem(v, AumlCompletionItemKind.Value, propertyType.Name))
            .ToList();
    }

    // Completes a type reference written as "[prefix:]Partial": offers the types of the prefix's xmlns (a
    // clr-namespace includes its view-models). Shared by {x:Type ...} and the plain type-valued x: directives.
    private IReadOnlyList<AumlCompletionItem> CompleteTypeNames(string prefixText, IReadOnlyDictionary<string, string> namespaces)
    {
        var (prefix, partial) = SplitName(prefixText);

        // Prefix already typed (e.g. "vm:Ma"): complete within that namespace, replacing only the name part.
        if (prefix.Length > 0)
        {
            var xmlns = ResolveXmlns(prefix, namespaces);
            if (xmlns.Length == 0) return [];
            return _model.GetElements(xmlns)
                .Where(t => Matches(t.Name, partial))
                .OrderBy(t => t.Name)
                .Select(t => new AumlCompletionItem(t.Name, AumlCompletionItemKind.Element, ReplaceBack: partial.Length))
                .ToList();
        }

        // No prefix yet: offer types from EVERY declared namespace so a view-model in a clr-namespace shows while you
        // type its bare name (without it, only the default xmlns was offered, so the list kept vanishing). A type
        // outside the default xmlns is inserted prefix-qualified ("vm:MainViewModel") so it resolves; filterText (the
        // bare name) keeps the client filtering the list as you type.
        var items = new List<AumlCompletionItem>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ns in namespaces)
        {
            foreach (var t in _model.GetElements(ns.Value))
            {
                if (!Matches(t.Name, partial)) continue;
                var insert = ns.Key.Length > 0 ? $"{ns.Key}:{t.Name}" : t.Name;
                if (!seen.Add(insert)) continue;
                items.Add(new AumlCompletionItem(t.Name, AumlCompletionItemKind.Element,
                    ns.Key.Length > 0 ? ns.Key : null, InsertText: insert, ReplaceBack: partial.Length));
            }
        }
        return items.OrderBy(i => i.Label).ToList();
    }

    // True when the attribute is an x: directive whose value names a CLR type (x:ViewModel), per the single-source
    // registry AumlDirectives. Lets the plain "prefix:Type" value complete types, like inside {x:Type}.
    private static bool IsTypeReferenceDirective(string? attributeName, IReadOnlyDictionary<string, string> namespaces)
    {
        if (string.IsNullOrEmpty(attributeName)) return false;
        int colon = attributeName.IndexOf(':');
        if (colon < 0) return false;
        if (!namespaces.TryGetValue(attributeName[..colon], out var ns) || ns != AumlXDirectives.Xmlns) return false;
        var local = attributeName[(colon + 1)..];
        return AumlDirectives.All.Any(d => d.Name == local && d.IsTypeReference);
    }

    // Properties whose markup value is a file path (the engine converts the string to the real resource on load).
    private static bool IsPathLike(Adamantium.UI.Markup.CodeGeneration.IResolvedType type) =>
        type.Name is "ImageSource" or "BitmapImage" or "BitmapSource" or "Uri";

    /// <summary>
    /// Completes a (possibly sub-foldered) file path written in an attribute value against the project root — the
    /// directory relative paths load against (the nearest <c>.csproj</c> ancestor of the edited file). The value
    /// so far is split into an already-typed directory part and a final-segment prefix; folders are offered with a
    /// trailing '/' so the path can be drilled into. Returns nothing without a known document/project.
    /// </summary>
    private IReadOnlyList<AumlCompletionItem> CompletePaths(string partial, string? documentPath)
    {
        var root = FindProjectRoot(documentPath);
        if (root is null) return [];

        partial = partial.Replace('\\', '/');
        int slash = partial.LastIndexOf('/');
        var dirPart = slash >= 0 ? partial[..(slash + 1)] : "";          // "Textures/" (kept, never re-inserted)
        var namePrefix = slash >= 0 ? partial[(slash + 1)..] : partial;  // the segment being typed

        string lookupDir;
        try { lookupDir = Path.GetFullPath(Path.Combine(root, dirPart)); }
        catch { return []; }
        if (!Directory.Exists(lookupDir)) return [];

        // Replace only the segment typed after the last '/' (so the client filters/replaces on the segment, not the
        // whole "Textures/..." value — otherwise bare file names get filtered out and nothing shows).
        var replaceBack = namePrefix.Length;
        var items = new List<AumlCompletionItem>();
        foreach (var dir in Directory.GetDirectories(lookupDir).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(dir);
            if (name is "bin" or "obj" || name.StartsWith('.') || !Matches(name, namePrefix)) continue;
            items.Add(new AumlCompletionItem(name + "/", AumlCompletionItemKind.Value, "folder", ReplaceBack: replaceBack));
        }
        foreach (var file in Directory.GetFiles(lookupDir).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            var name = Path.GetFileName(file);
            if (name.StartsWith('.') || !Matches(name, namePrefix)) continue;
            items.Add(new AumlCompletionItem(name, AumlCompletionItemKind.Value, "file", ReplaceBack: replaceBack));
        }
        return items;
    }

    // The project root a relative asset path resolves against: the nearest .csproj ancestor of the edited file.
    private static string? FindProjectRoot(string? documentPath)
    {
        if (string.IsNullOrEmpty(documentPath)) return null;
        DirectoryInfo? dir;
        try { dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(documentPath))!); }
        catch { return null; }
        for (; dir is not null; dir = dir.Parent)
            if (dir.GetFiles("*.csproj").Length > 0) return dir.FullName;
        return null;
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

    // An attribute completion that auto-inserts ="" and drops the caret between the quotes (LSP snippet),
    // so picking a property doesn't leave the author to type =" " themselves.
    private static AumlCompletionItem AttrItem(string label, AumlCompletionItemKind kind, string? detail) =>
        new(label, kind, detail, $"{label}=\"$0\"");

    // Substring match (case-insensitive): the typed text may appear ANYWHERE in the candidate, not only at the start,
    // so a half-remembered name still finds it ("Dash" -> StrokeDashArray, "Alignment" -> HorizontalAlignment, "ound"
    // -> Background). This only widens WHICH items the server offers; the matched letters are highlighted by the CLIENT
    // (LSP has no field to send highlight ranges - the client re-matches the typed text against filterText/label and
    // bolds the run). For PascalCase names both VS Code and Rider highlight the camel-boundary match out of the box;
    // to also keep matches that start mid-word (not on a word/camel boundary) VS Code needs
    // `editor.suggest.matchOnWordStartOnly: false`.
    private static bool Matches(string candidate, string prefix) =>
        string.IsNullOrEmpty(prefix) || candidate.Contains(prefix, StringComparison.OrdinalIgnoreCase);
}
