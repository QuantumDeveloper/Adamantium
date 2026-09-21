namespace Adamantium.UI.LanguageServer;

/// <summary>A single text replacement (0-based, end-exclusive) that a code action applies.</summary>
public sealed record AumlTextEdit(int StartLine, int StartCharacter, int EndLine, int EndCharacter, string NewText);

/// <summary>A quick-fix: a human-readable title and the edits that resolve the problem.</summary>
public sealed record AumlCodeAction(string Title, IReadOnlyList<AumlTextEdit> Edits);

/// <summary>
/// Auto-import quick-fixes for unresolved elements (the WPF/Avalonia "Import namespace" gesture):
/// when an element's type isn't found in its current xmlns but exists in another registered AUML
/// namespace, offer to either qualify it with an already-declared prefix, or declare a new xmlns
/// on the root and qualify the element. Works off the raw buffer so it tolerates mid-edit text.
/// </summary>
public static class CodeActionEngine
{
    public static IReadOnlyList<AumlCodeAction> CodeActions(string text, AumlTypeModel model, int offset)
    {
        var namespaces = AumlNamespaces.Scan(text);

        // An undeclared well-known directive prefix at the caret (e.g. x:Name after xmlns:x was deleted).
        if (DeclareKnownPrefixAction(text, offset, namespaces) is { } prefixAction)
            return new[] { prefixAction };

        return ElementImportActions(text, model, offset, namespaces);
    }

    private static IReadOnlyList<AumlCodeAction> ElementImportActions(
        string text, AumlTypeModel model, int offset, IReadOnlyDictionary<string, string> namespaces)
    {
        if (ElementNameAt(text, offset) is not { } element)
            return Array.Empty<AumlCodeAction>();

        var (nameStart, nameEnd, fullName) = element;
        var (elementPrefix, local) = SplitName(fullName);

        // Already resolves in its current namespace? Nothing to import.
        var currentXmlns = ResolveXmlns(elementPrefix, namespaces);
        if (currentXmlns.Length > 0 && model.GetElement(currentXmlns, local) is not null)
            return Array.Empty<AumlCodeAction>();

        var candidates = model.FindNamespacesContaining(local);
        if (candidates.Count == 0) return Array.Empty<AumlCodeAction>();

        var actions = new List<AumlCodeAction>();
        foreach (var uri in candidates)
        {
            var declaredPrefix = DeclaredPrefix(namespaces, uri);
            if (declaredPrefix is not null)
            {
                // The namespace is already imported — just qualify the element with its prefix.
                if (declaredPrefix == elementPrefix) continue;       // nothing would change
                var newName = declaredPrefix.Length == 0 ? local : $"{declaredPrefix}:{local}";
                actions.Add(new AumlCodeAction(
                    $"Qualify with '{newName}' (namespace already imported)",
                    new[] { Replace(text, nameStart, nameEnd, newName) }));
                continue;
            }

            // Not imported — declare it on the root element, then qualify if needed.
            int insertAt = RootTagNameEnd(text);
            if (insertAt < 0) continue;

            var edits = new List<AumlTextEdit>();
            if (elementPrefix.Length > 0 && !namespaces.ContainsKey(elementPrefix))
            {
                // Keep the prefix the author already typed (e.g. <controls:Border> -> declare 'controls').
                edits.Add(InsertRootAttribute(text, insertAt, $"xmlns:{elementPrefix}=\"{uri}\""));
                actions.Add(new AumlCodeAction($"Import '{uri}' for prefix '{elementPrefix}'", edits));
            }
            else if (elementPrefix.Length == 0 && !namespaces.ContainsKey(""))
            {
                // Unprefixed element with no default xmlns — declare it (resolves all unprefixed elements at once).
                edits.Add(InsertRootAttribute(text, insertAt, $"xmlns=\"{uri}\""));
                actions.Add(new AumlCodeAction($"Declare default xmlns '{uri}'", edits));
            }
            else
            {
                // A default (or this prefix) already maps elsewhere — import under a fresh prefix and qualify.
                var prefix = UniquePrefix(SuggestPrefix(uri), namespaces);
                edits.Add(InsertRootAttribute(text, insertAt, $"xmlns:{prefix}=\"{uri}\""));
                edits.Add(Replace(text, nameStart, nameEnd, $"{prefix}:{local}"));
                actions.Add(new AumlCodeAction($"Import '{uri}' (as '{prefix}:')", edits));
            }
        }

        return actions;
    }

    /// <summary>
    /// Offers to declare a well-known AUML directive prefix used at the caret but not declared
    /// (e.g. <c>x:Name</c> after <c>xmlns:x</c> was deleted). The x: namespace has no element types to
    /// look up, so we map its conventional prefix straight to its URI. Works on the raw buffer, so it
    /// fires even though the undeclared prefix makes the document invalid XML.
    /// </summary>
    private static AumlCodeAction? DeclareKnownPrefixAction(string text, int offset, IReadOnlyDictionary<string, string> namespaces)
    {
        if (QualifiedNameAt(text, offset) is not { } token) return null;
        var (prefix, _) = SplitName(token.Name);
        if (prefix.Length == 0 || namespaces.ContainsKey(prefix)) return null;
        if (KnownPrefixUri(prefix) is not { } uri) return null;

        int insertAt = RootTagNameEnd(text);
        if (insertAt < 0) return null;

        return new AumlCodeAction(
            $"Declare xmlns:{prefix}=\"{uri}\"",
            new[] { InsertRootAttribute(text, insertAt, $"xmlns:{prefix}=\"{uri}\"") });
    }

    /// <summary>
    /// Inserts a new attribute (xmlns) on the root tag. When the root already has attributes, the new
    /// one goes on the tag line and the existing first attribute moves to its own aligned line — the
    /// WPF/Avalonia layout — instead of crowding everything onto one line.
    /// </summary>
    private static AumlTextEdit InsertRootAttribute(string text, int nameEnd, string attribute)
    {
        int firstAttr = nameEnd;
        while (firstAttr < text.Length && char.IsWhiteSpace(text[firstAttr])) firstAttr++;

        // Bare tag (<Border> / <Border/>): nothing to align with, keep it on the same line.
        if (firstAttr >= text.Length || text[firstAttr] is '>' or '/')
            return InsertAt(text, nameEnd, $" {attribute}");

        var newline = text.Contains("\r\n") ? "\r\n" : "\n";
        int indent = PositionAt(text, firstAttr).Character;
        return InsertAt(text, firstAttr, $"{attribute}{newline}{new string(' ', indent)}");
    }

    /// <summary>Conventional URI for a well-known AUML prefix (only the type-less directive namespace).</summary>
    private static string? KnownPrefixUri(string prefix) => prefix == "x" ? AumlXDirectives.Xmlns : null;

    /// <summary>The qualified-name token (element or attribute) surrounding the caret, if any.</summary>
    private static (int Start, int End, string Name)? QualifiedNameAt(string text, int offset)
    {
        if (text.Length == 0) return null;
        int caret = Math.Clamp(offset, 0, text.Length);
        int start = caret;
        while (start > 0 && IsNameChar(text[start - 1])) start--;
        int end = caret;
        while (end < text.Length && IsNameChar(text[end])) end++;
        return end > start ? (start, end, text[start..end]) : null;
    }

    /// <summary>Prefix under which this URI is already declared, or null if it isn't ("" = default xmlns).</summary>
    private static string? DeclaredPrefix(IReadOnlyDictionary<string, string> namespaces, string uri)
    {
        foreach (var (prefix, declared) in namespaces)
            if (declared == uri) return prefix;
        return null;
    }

    private static string SuggestPrefix(string uri) => uri switch
    {
        "http://adamantium/ui" => "controls",
        "http://adamantium/ui/resources" => "resources",
        "http://adamantium/ui/xaml/extensions" => "x",
        _ => LastSegment(uri)
    };

    private static string LastSegment(string uri)
    {
        var trimmed = uri.TrimEnd('/');
        int slash = trimmed.LastIndexOf('/');
        var segment = new string((slash < 0 ? trimmed : trimmed[(slash + 1)..]).Where(char.IsLetter).ToArray());
        return segment.Length == 0 ? "ns" : segment.ToLowerInvariant();
    }

    private static string UniquePrefix(string preferred, IReadOnlyDictionary<string, string> namespaces)
    {
        if (!namespaces.ContainsKey(preferred)) return preferred;
        for (int n = 2; ; n++)
            if (!namespaces.ContainsKey($"{preferred}{n}")) return $"{preferred}{n}";
    }

    /// <summary>The offset just past the root element's name — where a new xmlns attribute is inserted.</summary>
    private static int RootTagNameEnd(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            int lt = text.IndexOf('<', i);
            if (lt < 0 || lt + 1 >= text.Length) return -1;

            char c = text[lt + 1];
            if (c == '?') { int e = text.IndexOf("?>", lt, StringComparison.Ordinal); if (e < 0) return -1; i = e + 2; continue; }
            if (c == '!') { int e = text.IndexOf('>', lt); if (e < 0) return -1; i = e + 1; continue; }
            if (char.IsLetter(c))
            {
                int j = lt + 1;
                while (j < text.Length && IsNameChar(text[j])) j++;
                return j;
            }
            i = lt + 1;
        }
        return -1;
    }

    private static (int Start, int End, string Name)? ElementNameAt(string text, int offset)
    {
        if (text.Length == 0) return null;
        offset = Math.Clamp(offset, 0, text.Length - 1);
        int lt = text.LastIndexOf('<', offset);
        if (lt < 0) return null;

        int i = lt + 1;
        if (i < text.Length && text[i] == '/') i++;     // closing tag
        int start = i;
        while (i < text.Length && IsNameChar(text[i])) i++;
        return i == start ? null : (start, i, text[start..i]);
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '.' or ':' or '-';

    private static AumlTextEdit Replace(string text, int start, int end, string newText)
    {
        var (sl, sc) = PositionAt(text, start);
        var (el, ec) = PositionAt(text, end);
        return new AumlTextEdit(sl, sc, el, ec, newText);
    }

    private static AumlTextEdit InsertAt(string text, int offset, string newText)
    {
        var (line, character) = PositionAt(text, offset);
        return new AumlTextEdit(line, character, line, character, newText);
    }

    private static (int Line, int Character) PositionAt(string text, int offset)
    {
        int line = 0, character = 0;
        for (int i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n') { line++; character = 0; }
            else character++;
        }
        return (line, character);
    }

    private static string ResolveXmlns(string prefix, IReadOnlyDictionary<string, string> namespaces) =>
        namespaces.TryGetValue(prefix, out var uri) ? uri : "";   // undeclared -> unresolved (so we offer to import)

    private static (string Prefix, string Local) SplitName(string name)
    {
        int colon = name.IndexOf(':');
        return colon < 0 ? ("", name) : (name[..colon], name[(colon + 1)..]);
    }
}
