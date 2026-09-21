using Adamantium.UI.Markup.CodeGeneration;
using Adamantium.UI.Markup.CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// Resolves the C# declaration of the element/attribute under the caret (go-to-definition). In-repo types —
/// including the engine base controls, which the source graph compiles from source — resolve to their real
/// source file; types that live only in an external assembly are decompiled on demand (see
/// <see cref="MetadataDecompiler"/>). Reuses the hover token detector by placing the caret at the token end.
/// </summary>
public sealed class DefinitionEngine
{
    private const string FallbackXmlns = "http://adamantium/ui";

    private readonly AumlTypeModel _model;

    public DefinitionEngine(AumlTypeModel model) => _model = model;

    public DefinitionLocation? Definition(string text, int offset)
    {
        if (string.IsNullOrEmpty(text) || offset < 0 || offset > text.Length) return null;

        int tokenEnd = offset;
        while (tokenEnd < text.Length && IsNameChar(text[tokenEnd])) tokenEnd++;

        var namespaces = AumlNamespaces.Scan(text);
        var ctx = AumlCaretContext.Detect(text, tokenEnd);
        var symbol = ctx.Kind switch
        {
            AumlCompletionKind.ElementName => ElementSymbol(ctx.Prefix, namespaces),
            AumlCompletionKind.AttributeName => AttributeSymbol(ctx, namespaces),
            _ => null
        };
        return symbol is null ? null : Locate(symbol);
    }

    // An element name is a type ("controls:Border") or a property-element ("controls:Border.Child"); the latter
    // navigates to the property, not the type.
    private ISymbol? ElementSymbol(string? qualifiedName, IReadOnlyDictionary<string, string> namespaces)
    {
        var name = qualifiedName ?? "";
        int dot = name.IndexOf('.');
        if (dot < 0)
            return SymbolOf(ResolveElement(name, namespaces));

        var owner = ResolveElement(name[..dot], namespaces);
        return MemberSymbol(owner, name[(dot + 1)..]);
    }

    private ISymbol? AttributeSymbol(AumlCompletionContext ctx, IReadOnlyDictionary<string, string> namespaces)
    {
        var (attrPrefix, local) = SplitName(ctx.Prefix ?? "");

        // x: directive — a markup language feature, no C# declaration to jump to.
        if (attrPrefix.Length > 0 && namespaces.TryGetValue(attrPrefix, out var ns) && ns == AumlXDirectives.Xmlns)
            return null;

        // Attached property "Owner.Prop" — the owner is the dotted prefix; jump to its Get<Prop> accessor.
        int dot = local.IndexOf('.');
        if (dot >= 0)
            return MemberSymbol(_model.FindElement(local[..dot]), local[(dot + 1)..], attached: true);

        return MemberSymbol(ResolveElement(ctx.ElementName, namespaces), local);
    }

    private DefinitionLocation? Locate(ISymbol symbol)
    {
        foreach (var location in symbol.Locations)
        {
            if (!location.IsInSource || location.SourceTree is null) continue;
            var span = location.GetLineSpan();
            return new DefinitionLocation(location.SourceTree.FilePath,
                span.StartLinePosition.Line, span.StartLinePosition.Character,
                span.EndLinePosition.Line, span.EndLinePosition.Character);
        }

        // No source (external assembly) — decompile the type and land on the member.
        return MetadataDecompiler.Locate(symbol, _model.Compilation);
    }

    private IResolvedType? ResolveElement(string? qualifiedName, IReadOnlyDictionary<string, string> namespaces)
    {
        var (prefix, local) = SplitName(qualifiedName ?? "");
        var xmlns = ResolveXmlns(prefix, namespaces);
        return xmlns.Length == 0 ? null : _model.GetElement(xmlns, local);
    }

    private static ISymbol? MemberSymbol(IResolvedType? owner, string name, bool attached = false)
    {
        if (owner is null) return null;
        // Attached properties expose Get<Name>/Set<Name> accessors; prefer the getter's declaration.
        var member = (attached ? owner.GetMemberByName("Get" + name) : null)
                     ?? owner.GetMemberByName(name)
                     ?? owner.GetMemberByName("Get" + name);
        return SymbolOf(member);
    }

    private static ISymbol? SymbolOf(IResolvedType? type) => (type as RoslynResolvedType)?.Symbol;

    private static ISymbol? SymbolOf(IResolvedMember? member) => (member as RoslynResolvedMember)?.Symbol;

    private static string ResolveXmlns(string prefix, IReadOnlyDictionary<string, string> namespaces) =>
        namespaces.TryGetValue(prefix, out var uri) ? uri : prefix.Length == 0 ? FallbackXmlns : "";

    private static (string Prefix, string Local) SplitName(string name)
    {
        int colon = name.IndexOf(':');
        return colon < 0 ? ("", name) : (name[..colon], name[(colon + 1)..]);
    }

    private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or '-' or '.' or ':';
}
