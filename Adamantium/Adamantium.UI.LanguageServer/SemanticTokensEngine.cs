namespace Adamantium.UI.LanguageServer;

/// <summary>A semantic-highlighting token: a byte span in the buffer and its LSP token-type index.</summary>
public sealed record SemToken(int Start, int Length, int TokenType);

/// <summary>
/// Type-aware highlighting for AUML. A lenient tag scanner (no full parse, so it survives mid-edit
/// buffers — and works even when an undeclared prefix makes the doc invalid XML) classifies element
/// names, xmlns prefixes, attribute names and x: directives. An element name that resolves gets
/// 'type'; one that doesn't gets 'unknown' (painted red by the client, like ReSharper's unresolved
/// types), so deleting an xmlns turns the controls red. Indices match the server's advertised legend.
/// </summary>
public static class SemanticTokensEngine
{
    public const int Namespace = 0, Type = 1, Property = 2, Macro = 3, Unknown = 4;

    public static IReadOnlyList<SemToken> Tokenize(string text, AumlTypeModel? model)
    {
        var namespaces = AumlNamespaces.Scan(text);
        var tokens = new List<SemToken>();
        int i = 0;
        while (i < text.Length)
        {
            if (text[i] != '<') { i++; continue; }

            // Skip comments / processing instructions / CDATA / declarations.
            if (StartsWith(text, i, "<!--")) { i = After(text, "-->", i); continue; }
            if (StartsWith(text, i, "<?")) { i = After(text, "?>", i); continue; }
            if (StartsWith(text, i, "<![CDATA[")) { i = After(text, "]]>", i); continue; }
            if (i + 1 < text.Length && text[i + 1] == '!') { i = After(text, ">", i); continue; }

            i++;                                              // past '<'
            if (i < text.Length && text[i] == '/') i++;       // closing tag

            int nameStart = i;
            while (i < text.Length && IsNameChar(text[i])) i++;
            if (i > nameStart) AddName(tokens, text, nameStart, i, element: true, namespaces, model);

            // Attributes until the end of the tag.
            while (i < text.Length && text[i] != '>')
            {
                char c = text[i];
                if (c is '"' or '\'')                          // a quoted value
                {
                    int valStart = ++i;
                    while (i < text.Length && text[i] != c) i++;
                    TokenizeValue(tokens, text, valStart, i, namespaces, model);   // colour {Binding}/{x:Type}/… inside
                    if (i < text.Length) i++;                  // past the closing quote
                    continue;
                }
                if (IsNameStart(c))
                {
                    int attrStart = i;
                    while (i < text.Length && IsNameChar(text[i])) i++;
                    int j = i;
                    while (j < text.Length && char.IsWhiteSpace(text[j])) j++;
                    // Only an attribute name (followed by '='); xmlns declarations are left to XML syntax colouring.
                    if (j < text.Length && text[j] == '=' && !StartsWith(text, attrStart, "xmlns"))
                        AddName(tokens, text, attrStart, i, element: false, namespaces, model);
                    continue;
                }
                i++;
            }
            if (i < text.Length) i++;                          // past '>'
        }
        return tokens;
    }

    private static void AddName(List<SemToken> tokens, string text, int start, int end, bool element,
        IReadOnlyDictionary<string, string> namespaces, AumlTypeModel? model)
    {
        int colon = -1;
        for (int k = start; k < end; k++) if (text[k] == ':') { colon = k; break; }

        var prefix = colon >= 0 ? text[start..colon] : "";
        if (colon >= 0) tokens.Add(new SemToken(start, colon - start, Namespace));

        int localStart = colon >= 0 ? colon + 1 : start;
        int localLength = end - localStart;
        if (localLength <= 0) return;

        int type;
        if (element)
        {
            // Property-element syntax <Owner.Property> (e.g. <RenderTargetPanel.Behaviors>): the tag name is not a
            // type, it's a type plus a property. Colour the owner like a type and the trailing .Property like a
            // property, instead of trying (and failing) to resolve "Owner.Property" as a type and painting the
            // whole tag red (Unknown). Split on the first '.', since the owner is always a simple type name.
            int dot = -1;
            for (int k = localStart; k < end; k++) if (text[k] == '.') { dot = k; break; }
            if (dot > localStart && dot + 1 < end)
            {
                int ownerLength = dot - localStart;
                var ownerXmlns = namespaces.TryGetValue(prefix, out var ownerUri) ? ownerUri : "";
                int ownerType = model is null
                    || (ownerXmlns.Length > 0 && model.GetElement(ownerXmlns, text.Substring(localStart, ownerLength)) is not null)
                        ? Type
                        : Unknown;
                tokens.Add(new SemToken(localStart, ownerLength, ownerType));
                tokens.Add(new SemToken(dot + 1, end - (dot + 1), Property));
                return;
            }

            // A resolved type -> 'type'; an unresolved one -> 'unknown' (client paints it red). When the
            // project model is unavailable we can't tell, so fall back to 'type' (no false red).
            if (model is null)
                type = Type;
            else
            {
                var xmlns = namespaces.TryGetValue(prefix, out var uri) ? uri : "";
                type = xmlns.Length > 0 && model.GetElement(xmlns, text.Substring(localStart, localLength)) is not null
                    ? Type
                    : Unknown;
            }
        }
        else
        {
            type = prefix == "x" ? Macro : Property;
        }

        tokens.Add(new SemToken(localStart, localLength, type));
    }

    // Colours a markup extension written inside an attribute value (e.g. "{Binding ShowMessageCommand}",
    // "{x:Type vm:MainViewModel}"); a plain string value is left to the client's default string colour.
    private static void TokenizeValue(List<SemToken> tokens, string text, int start, int end,
        IReadOnlyDictionary<string, string> namespaces, AumlTypeModel? model)
    {
        int i = start;
        while (i < end && char.IsWhiteSpace(text[i])) i++;
        if (i < end && text[i] == '{') TokenizeMarkupExtension(tokens, text, i, end, namespaces, model);
    }

    private static void TokenizeMarkupExtension(List<SemToken> tokens, string text, int start, int end,
        IReadOnlyDictionary<string, string> namespaces, AumlTypeModel? model)
    {
        int i = start + 1;   // past '{'
        while (i < end && char.IsWhiteSpace(text[i])) i++;

        // Extension name (Binding / x:Type / ResourceReference / …): the local part reads as a 'macro', like an
        // x: directive; an x: prefix on it reads as a namespace.
        int nameStart = i;
        while (i < end && IsNameChar(text[i])) i++;
        if (i > nameStart) AddQualifiedName(tokens, text, nameStart, i, bareKind: Macro, prefixedKind: Macro);

        // Body: type references (prefix:Type -> 'type'), binding paths (bare identifiers -> 'property'), nested
        // extensions ({StaticResource …}). Bounded by 'end' (the closing quote), so a missing '}' mid-edit is safe.
        while (i < end)
        {
            char c = text[i];
            if (c == '}') break;
            if (c == '{')
            {
                int close = i + 1, depth = 1;
                while (close < end && depth > 0) { if (text[close] == '{') depth++; else if (text[close] == '}') depth--; close++; }
                TokenizeMarkupExtension(tokens, text, i, close, namespaces, model);
                i = close;
                continue;
            }
            if (IsNameStart(c))
            {
                int s = i;
                while (i < end && IsNameChar(text[i])) i++;
                AddQualifiedName(tokens, text, s, i, bareKind: Property, prefixedKind: Type);
                continue;
            }
            i++;
        }
    }

    // Colours a "[prefix:]name" token: the prefix as a namespace; the local part as prefixedKind when it has a prefix
    // (a value's prefix:name is always a type reference), else bareKind.
    private static void AddQualifiedName(List<SemToken> tokens, string text, int start, int end, int bareKind, int prefixedKind)
    {
        int colon = -1;
        for (int k = start; k < end; k++) if (text[k] == ':') { colon = k; break; }
        if (colon >= 0)
        {
            if (colon > start) tokens.Add(new SemToken(start, colon - start, Namespace));
            if (colon + 1 < end) tokens.Add(new SemToken(colon + 1, end - (colon + 1), prefixedKind));
        }
        else
        {
            tokens.Add(new SemToken(start, end - start, bareKind));
        }
    }

    private static bool IsNameStart(char c) => char.IsLetter(c) || c == '_';
    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or ':' or '-';

    private static bool StartsWith(string text, int i, string value) =>
        i + value.Length <= text.Length && text.AsSpan(i, value.Length).SequenceEqual(value);

    private static int After(string text, string marker, int from)
    {
        int idx = text.IndexOf(marker, from, StringComparison.Ordinal);
        return idx < 0 ? text.Length : idx + marker.Length;
    }
}
