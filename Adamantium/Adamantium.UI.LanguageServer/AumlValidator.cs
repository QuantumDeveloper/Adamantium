using System.Text.RegularExpressions;
using Adamantium.Core;
using Adamantium.UI.Markup.AST;
using Adamantium.UI.Markup.CodeGeneration;
using Adamantium.UI.Markup.Parsers;

namespace Adamantium.UI.LanguageServer;

/// <summary>A diagnostic span (0-based line/character) and message for an AUML problem.</summary>
public sealed record AumlDiagnostic(int Line, int Character, int Length, string Message);

/// <summary>
/// Walks a well-formed AUML document and validates element names, attribute names, and enum
/// values against the project's type model. Validates every element whose xmlns is a registered
/// AUML namespace (default, controls, resources, …) and descends into property-element values
/// (e.g. <c>&lt;Setter.Value&gt;&lt;ControlTemplate&gt;…</c>), so it covers real themes/templates.
/// Deliberately conservative — it only flags things that definitely don't exist, and skips
/// malformed/incomplete buffers (the editor's own XML support covers syntax).
/// </summary>
public static class AumlValidator
{
    public static IReadOnlyList<AumlDiagnostic> Validate(string text, AumlTypeModel model)
    {
        var diagnostics = new List<AumlDiagnostic>();

        // Undeclared well-known directive prefix (x: with no xmlns:x). Raw-text scan, because such a
        // prefix is invalid XML and the parser below would just throw past it — yet we still want a
        // diagnostic so the "Declare xmlns:x" quick-fix has something to anchor on.
        ValidateKnownPrefixes(text, diagnostics);

        AumlDocument document;
        try { document = AumlParser.Parse(text); }
        catch { return diagnostics; }            // malformed/incomplete (maybe the prefix above) — return what we have

        // Walk the tree whenever there is one — even a missing root xmlns leaves a usable AST, and a
        // per-element "not in scope" diagnostic (with an import quick-fix) beats a generic parse message.
        if (document.Root is AumlAstObjectNode root)
        {
            Walk(root, model, diagnostics);
            ValidateClrNamespaces(text, model, diagnostics);
        }

        // Surface raw AUML parse errors only when the walk found nothing better (avoids double-flagging
        // the root with both "xmlns missing" and "not in scope").
        if (diagnostics.Count == 0 && document.HasErrors && document.Logger?.Messages is { } messages)
            foreach (var message in messages)
                if (message.Type == LogMessageType.Error)
                    diagnostics.Add(new AumlDiagnostic(0, 0, 1, message.Text));

        return diagnostics;
    }

    // The x: directive prefix used as an element/attribute name (preceded by '<' or whitespace, so we
    // don't match "x:" inside quoted values), but not declared via xmlns:x.
    private static readonly Regex KnownPrefixUsage = new(@"(?<=[<\s])x:[A-Za-z_]", RegexOptions.Compiled);

    private static void ValidateKnownPrefixes(string text, List<AumlDiagnostic> diagnostics)
    {
        if (AumlNamespaces.Scan(text).ContainsKey("x")) return;   // declared -> nothing to flag

        foreach (Match match in KnownPrefixUsage.Matches(text))
        {
            var (line, character) = LineColAt(text, match.Index);
            diagnostics.Add(new AumlDiagnostic(line, character, 1, "Namespace prefix 'x' is not declared (add xmlns:x)"));
        }
    }

    private static readonly Regex ClrXmlnsDeclaration = new(
        @"xmlns(?::[\w.\-]+)?\s*=\s*""(?<uri>clr-namespace:[^""]*)""", RegexOptions.Compiled);

    /// <summary>
    /// Flags <c>clr-namespace:</c> xmlns declarations whose CLR namespace (and optional assembly)
    /// isn't found in the project's references — Rider's XML support can't validate these, so the
    /// language server does (the matching highlight filter suppresses XML's "URI is not registered").
    /// </summary>
    private static void ValidateClrNamespaces(string text, AumlTypeModel model, List<AumlDiagnostic> diagnostics)
    {
        foreach (Match match in ClrXmlnsDeclaration.Matches(text))
        {
            var uri = match.Groups["uri"];
            if (model.GetElements(uri.Value).Count > 0) continue;     // resolves to a real CLR namespace

            var (line, character) = LineColAt(text, uri.Index);
            diagnostics.Add(new AumlDiagnostic(line, character, uri.Length, $"CLR namespace not found: '{uri.Value}'"));
        }
    }

    private static (int Line, int Character) LineColAt(string text, int offset)
    {
        int line = 0, character = 0;
        for (int i = 0; i < offset && i < text.Length; i++)
        {
            if (text[i] == '\n') { line++; character = 0; }
            else character++;
        }
        return (line, character);
    }

    private static void Walk(AumlAstObjectNode node, AumlTypeModel model, List<AumlDiagnostic> diagnostics)
    {
        var xmlns = node.TypeReference.Namespace;
        var name = node.TypeReference.Name;

        IResolvedType? element = null;
        if (model.IsKnownNamespace(xmlns))
        {
            element = model.GetElement(xmlns, name);
            if (element is null)
                diagnostics.Add(At(node, name.Length, $"Unknown element '{name}'"));
        }
        else if (string.IsNullOrEmpty(xmlns) && model.FindElement(name) is not null)
        {
            // Unprefixed element whose xmlns isn't declared, but the type exists in a known namespace
            // (e.g. a deleted default xmlns) — the auto-import quick-fix offers to declare it.
            diagnostics.Add(At(node, name.Length, $"'{name}' is not in scope — its xmlns is not declared"));
        }

        foreach (var property in node.GetProperties())
        {
            if (element is not null
                && property.Property is AumlAstPropertyReference reference
                && !reference.IsAttachedProperty)
            {
                ValidateAttribute(reference, property, name, element, model, diagnostics);
            }

            // Descend into property-element values: <Setter.Value><ControlTemplate>... etc.
            foreach (var value in property.Values)
                if (value is AumlAstObjectNode nested)
                    Walk(nested, model, diagnostics);
        }

        foreach (var child in node.GetLogicalChildrenObjects())
            Walk(child, model, diagnostics);
    }

    private static void ValidateAttribute(AumlAstPropertyReference reference, AumlAstPropertyNode property,
        string elementName, IResolvedType element, AumlTypeModel model, List<AumlDiagnostic> diagnostics)
    {
        var propertyName = reference.Name;
        if (!model.IsKnownAttribute(element, propertyName))
        {
            diagnostics.Add(At(reference, propertyName.Length, $"Unknown property '{propertyName}' on '{elementName}'"));
            return;
        }

        // Enum value check; skip flags combos and numeric forms to avoid false positives.
        var propertyType = model.GetPropertyType(element, propertyName);
        var value = property.Values.FirstOrDefault();
        if (propertyType is { TypeKind: ResolvedTypeKind.Enum } && value is not null && value.IsTextNode())
        {
            var valueText = value.GetTextValue();
            if (valueText.Length > 0 && !valueText.Contains(',') && !valueText.All(char.IsDigit))
            {
                var allowed = model.GetValueCompletions(propertyType);
                if (!allowed.Contains(valueText))
                    diagnostics.Add(At(reference, propertyName.Length,
                        $"'{valueText}' is not a valid {propertyType.Name} (expected: {string.Join(", ", allowed)})"));
            }
        }
    }

    private static AumlDiagnostic At(AumlAstNode node, int length, string message) =>
        new(Math.Max(0, node.Line - 1), Math.Max(0, node.Position - 1), Math.Max(1, length), message);
}
