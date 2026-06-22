using Adamantium.UI.Markup.CodeGeneration;
using Adamantium.UI.Markup.CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Adamantium.UI.LanguageServer;

/// <summary>A markup-settable property: the name written as an attribute, plus its CLR type.</summary>
public sealed record AumlPropertyInfo(string Name, IResolvedType Type);

/// <summary>
/// AUML type model: builds a Roslyn compilation from the target project's referenced
/// assemblies and reuses the engine's own <see cref="RoslynTypeResolver"/>, so completion
/// sees exactly the types the AUML source generator sees.
/// </summary>
public sealed class AumlTypeModel
{
    private readonly ITypeResolver _resolver;
    private readonly Dictionary<string, IReadOnlyList<IResolvedType>> _clrNamespaceCache = new(StringComparer.Ordinal);

    private AumlTypeModel(ITypeResolver resolver, Compilation compilation)
    {
        _resolver = resolver;
        Compilation = compilation;
    }

    /// <summary>The backing compilation — go-to-definition uses it to map a metadata symbol back to its source dll.</summary>
    public Compilation Compilation { get; }

    public static AumlTypeModel Build(IEnumerable<string> assemblyPaths, IEnumerable<string> sourceFiles = null)
    {
        var references = new List<MetadataReference>();
        foreach (var path in assemblyPaths)
        {
            if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
            try { references.Add(MetadataReference.CreateFromFile(path)); }
            catch { /* native or otherwise non-managed dll — skip */ }
        }

        // The project's own C# source is compiled in as syntax trees so its types/properties resolve live
        // from source (no build needed) — its compiled dll is excluded from the references by the caller so a
        // type isn't defined twice. Type lookups go through Compilation.GetTypeByMetadataName, which is
        // source-aware, so bindings/property-type completion reflect saved source edits immediately.
        var syntaxTrees = new List<SyntaxTree>();
        if (sourceFiles is not null)
        {
            foreach (var file in sourceFiles)
            {
                try { syntaxTrees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(file), path: file)); }
                catch { /* unreadable/locked file — skip */ }
            }
        }

        var compilation = CSharpCompilation.Create(
            "AumlTooling",
            syntaxTrees,
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return FromCompilation(compilation);
    }

    /// <summary>
    /// Wraps a ready compilation (e.g. the source-graph root from <see cref="SourceProjectGraph"/>). The
    /// optional <paramref name="xmlnsMappings"/> are xmlns -> assembly mappings read from sub-compilations
    /// (where their constructor arguments are materialized) and injected, since they can't be read back through
    /// a CompilationReference.
    /// </summary>
    public static AumlTypeModel FromCompilation(
        Compilation compilation, IEnumerable<(string XmlNamespace, string ClrSpec)> xmlnsMappings = null)
    {
        var resolver = new RoslynTypeResolver(compilation);
        resolver.ScanXmlnsAttributes();
        if (xmlnsMappings is not null)
            foreach (var (xmlNamespace, clrSpec) in xmlnsMappings)
                resolver.AddXmlnsMapping(xmlNamespace, clrSpec);
        return new AumlTypeModel(resolver, compilation);
    }

    /// <summary>
    /// Element types available under an xmlns: an [XmlnsDefinition] URI (e.g. "http://adamantium/ui")
    /// or a XAML-style "clr-namespace:Some.Ns[;assembly=Asm]" mapping onto a raw CLR namespace.
    /// </summary>
    public IReadOnlyList<IResolvedType> GetElements(string xmlns)
    {
        if (TryParseClrNamespace(xmlns, out var clrNamespace, out var assemblyName))
            return GetClrNamespaceTypes(xmlns, clrNamespace, assemblyName);

        var assembly = _resolver.GetResolvedAssemblyByXmlDefinition(xmlns);
        return assembly is null
            ? []
            : assembly.Types.Where(IsMarkupType).ToList();
    }

    // Compiler-generated types (<Module>, <>c, <PrivateImplementationDetails>, <>z__ReadOnlyArray, …) are never
    // valid markup elements; their names start with '<' (inexpressible in C#), so drop them from completion.
    private static bool IsMarkupType(IResolvedType type) =>
        !string.IsNullOrEmpty(type.Name) && type.Name[0] != '<';

    /// <summary>
    /// Resolves the types of a <c>clr-namespace:</c> xmlns — scoped to <c>;assembly=</c> when given,
    /// otherwise the first referenced assembly that declares the CLR namespace. Cached per URI, since
    /// the no-assembly lookup may enumerate every referenced assembly.
    /// </summary>
    private IReadOnlyList<IResolvedType> GetClrNamespaceTypes(string uri, string clrNamespace, string assemblyName)
    {
        if (_clrNamespaceCache.TryGetValue(uri, out var cached)) return cached;

        var assembly = string.IsNullOrEmpty(assemblyName)
            ? _resolver.FindAssemblyByNamespace(clrNamespace)
            : _resolver.GetResolvedAssembly(assemblyName) ?? _resolver.ResolveAssembly(assemblyName);

        IReadOnlyList<IResolvedType> types = assembly is null
            ? []
            : assembly.Types.Where(t => t.Namespace == clrNamespace && IsMarkupType(t)).ToList();

        _clrNamespaceCache[uri] = types;
        return types;
    }

    /// <summary>Parses <c>clr-namespace:Some.Ns[;assembly=Asm]</c>; false when the URI isn't a clr-namespace.</summary>
    private static bool TryParseClrNamespace(string uri, out string clrNamespace, out string assembly)
    {
        clrNamespace = "";
        assembly = "";
        const string scheme = "clr-namespace:";
        if (!uri.StartsWith(scheme, StringComparison.Ordinal)) return false;

        var rest = uri[scheme.Length..];
        int semicolon = rest.IndexOf(';');
        if (semicolon < 0)
        {
            clrNamespace = rest.Trim();
            return true;
        }

        clrNamespace = rest[..semicolon].Trim();
        const string assemblyKey = "assembly=";
        int key = rest.IndexOf(assemblyKey, semicolon, StringComparison.Ordinal);
        if (key >= 0) assembly = rest[(key + assemblyKey.Length)..].Trim();
        return true;
    }

    public IResolvedType? GetElement(string xmlns, string name) =>
        GetElements(xmlns).FirstOrDefault(t => t.Name == name);

    /// <summary>
    /// Every registered xmlns whose assembly contains an element type with this simple name —
    /// the candidate namespaces to import for an unresolved element (the auto-import quick-fix).
    /// </summary>
    public IReadOnlyList<string> FindNamespacesContaining(string typeName)
    {
        var result = new List<string>();
        foreach (var xmlns in _resolver.XmlnsDefinitions)
            if (GetElement(xmlns, typeName) is not null)
                result.Add(xmlns);
        return result;
    }

    /// <summary>
    /// True if the xmlns is resolvable: a registered [XmlnsDefinition] namespace, or a
    /// <c>clr-namespace:</c> that maps onto a CLR namespace containing at least one type.
    /// </summary>
    public bool IsKnownNamespace(string xmlns) =>
        TryParseClrNamespace(xmlns, out _, out _)
            ? GetElements(xmlns).Count > 0
            : _resolver.GetResolvedAssemblyByXmlDefinition(xmlns) is not null;

    /// <summary>
    /// Markup-settable properties of an element type: public-settable, inherited included,
    /// de-duplicated by name, excluding explicit interface implementations (e.g. "IFoo.Bar").
    /// </summary>
    public IReadOnlyList<AumlPropertyInfo> GetProperties(IResolvedType element, bool includeReadOnlyCollections = false)
    {
        var result = new List<AumlPropertyInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.GetAllProperties() ?? Enumerable.Empty<IResolvedProperty>())
        {
            if (property.Name.Contains('.')) continue;   // explicit interface implementation
            if (property.Name.Contains('[')) continue;   // indexer (e.g. "this[]") — never valid markup
            if (!seen.Add(property.Name)) continue;       // already added from a more-derived type
            var member = element.GetMemberByName(property.Name);
            if (member is not { MemberKind: ResolvedMemberKind.Property }) continue;

            // Settable (as an attribute), plus — for property-element syntax — read-only collections
            // that are populated by adding children rather than assigned, e.g. Theme.StyleIncludes.
            if (member.HasSetter() ||
                (includeReadOnlyCollections && property.PropertyType is { } type && type.IsCollection()))
                result.Add(new AumlPropertyInfo(property.Name, property.PropertyType));
        }
        return result;
    }

    public IResolvedType? GetPropertyType(IResolvedType element, string propertyName) =>
        GetProperties(element).FirstOrDefault(p => p.Name == propertyName)?.Type;

    /// <summary>
    /// Readable properties for binding-path completion (<c>{Binding ...}</c> against an <c>x:ViewModel</c>): every
    /// public instance property, inherited included, de-duplicated by name — unlike <see cref="GetProperties"/>
    /// this does not require a setter, since one-way bindings read get-only properties too.
    /// </summary>
    public IReadOnlyList<AumlPropertyInfo> GetBindableProperties(IResolvedType type)
    {
        var result = new List<AumlPropertyInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in type.GetAllProperties() ?? Enumerable.Empty<IResolvedProperty>())
        {
            if (property.Name.Contains('.') || property.Name.Contains('[')) continue;   // explicit impl / indexer
            if (!seen.Add(property.Name)) continue;
            var member = type.GetMemberByName(property.Name);
            if (member is not { MemberKind: ResolvedMemberKind.Property }) continue;
            result.Add(new AumlPropertyInfo(property.Name, property.PropertyType));
        }
        AddGeneratedMvvmMembers(type, result, seen);
        return result;
    }

    // The no-build type model doesn't run source generators, so members the Adamantium.MVVM generator would emit
    // aren't real symbols. Synthesize them from the attributes the author wrote (matching the generator's naming) so
    // {Binding} still completes them: [Command] method M -> "MCommand"; [Bindable] field _x -> property "X".
    private static void AddGeneratedMvvmMembers(IResolvedType type, List<AumlPropertyInfo> result, HashSet<string> seen)
    {
        const string commandAttr = "Adamantium.MVVM.CommandAttribute";
        const string bindableAttr = "Adamantium.MVVM.BindableAttribute";
        foreach (var member in type.Members ?? Enumerable.Empty<IResolvedMember>())
        {
            if (member.MemberKind == ResolvedMemberKind.Method && member.HasAttribute(commandAttr))
            {
                var name = member.Name + "Command";
                if (seen.Add(name)) result.Add(new AumlPropertyInfo(name, null));
            }
            else if (member.MemberKind == ResolvedMemberKind.Field && member.HasAttribute(bindableAttr))
            {
                var name = MvvmPropertyName(member.Name);
                if (name is not null && seen.Add(name)) result.Add(new AumlPropertyInfo(name, member.MemberType));
            }
        }
    }

    // Field-name -> generated property name, as the MVVM generator does it: drop a leading "_" or "m_", upper-case the
    // first letter. "_title"/"m_title"/"title" -> "Title".
    private static string MvvmPropertyName(string fieldName)
    {
        var n = fieldName;
        if (n.StartsWith("m_", StringComparison.Ordinal)) n = n[2..];
        else if (n.StartsWith("_", StringComparison.Ordinal)) n = n[1..];
        if (n.Length == 0) return null;
        return char.ToUpperInvariant(n[0]) + n[1..];
    }

    /// <summary>First element type with this simple name across all registered xmlns namespaces — used
    /// to resolve an attached-property owner written without an xmlns prefix (e.g. <c>ResourceContext</c>).</summary>
    public IResolvedType? FindElement(string name)
    {
        foreach (var xmlns in _resolver.XmlnsDefinitions)
            if (GetElement(xmlns, name) is { } type) return type;
        return null;
    }

    /// <summary>
    /// Attached properties an owner type declares (XAML <c>Owner.Property</c> syntax): a static
    /// <c>Get&lt;Name&gt;</c>/<c>Set&lt;Name&gt;</c> accessor pair (e.g. ResourceContext.Source from
    /// GetSource/SetSource). Only members declared on the owner — never inherited DP accessors like
    /// GetValue/SetValue — so it doesn't mistake instance plumbing for attached properties.
    /// </summary>
    public IReadOnlyList<AumlPropertyInfo> GetAttachedProperties(IResolvedType owner)
    {
        var members = owner.Members.Where(m => m.MemberKind == ResolvedMemberKind.Method).ToList();
        var setters = new HashSet<string>(
            members.Where(m => m.Name.StartsWith("Set", StringComparison.Ordinal) && m.Name.Length > 3)
                   .Select(m => m.Name[3..]),
            StringComparer.Ordinal);

        var result = new List<AumlPropertyInfo>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var getter in members)
        {
            if (!getter.Name.StartsWith("Get", StringComparison.Ordinal) || getter.Name.Length <= 3) continue;
            var name = getter.Name[3..];
            if (!setters.Contains(name) || !seen.Add(name)) continue;
            result.Add(new AumlPropertyInfo(name, getter.MemberType));   // getter return type = the property's value type
        }
        return result;
    }

    /// <summary>
    /// True if the element type has any member with this name (property, event, etc.). Used by
    /// diagnostics to flag only clearly-unknown attributes, never valid-but-unusual members.
    /// </summary>
    public bool IsKnownAttribute(IResolvedType element, string name) =>
        element.GetMemberByName(name) is not null;

    /// <summary>Value completions for an attribute's CLR type: enum members or boolean literals.</summary>
    public IReadOnlyList<string> GetValueCompletions(IResolvedType propertyType)
    {
        if (propertyType.TypeKind == ResolvedTypeKind.Enum)
            return propertyType.Members
                .Where(m => m.MemberKind == ResolvedMemberKind.Field && m.Name != "value__")
                .Select(m => m.Name)
                .ToList();

        if (propertyType.SpecialType == ResolvedSpecialType.System_Boolean)
            return ["true", "false"];

        if (propertyType.Name is "Brush" or "IBrush")
            return CommonColors;

        return [];
    }

    /// <summary>Names of usable markup extensions (<c>{Name ...}</c>): every type deriving from MarkupExtension,
    /// with the conventional "Extension" suffix stripped (e.g. BitmapImageExtension -> BitmapImage).</summary>
    public IReadOnlyList<string> GetMarkupExtensions()
    {
        const string baseFqn = "Adamantium.UI.Core.MarkupExtensions.MarkupExtension";
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var assembly in _resolver.ResolvedAssemblies)
            foreach (var type in assembly.Types)
            {
                if (type.Name == "MarkupExtension" || !type.InheritsFromMarkupExtension(baseFqn)) continue;
                var name = type.Name;
                if (name.EndsWith("Extension") && name.Length > "Extension".Length) name = name[..^"Extension".Length];
                names.Add(name);
            }
        return names.ToList();
    }

    /// <summary>Resolves a markup-extension type from the name used in <c>{Name ...}</c>: matches either the exact type
    /// name or the conventional <c>NameExtension</c> form (so both <c>{Binding}</c> and <c>{BindingExtension}</c> work).</summary>
    public IResolvedType? ResolveMarkupExtensionType(string localName)
    {
        const string baseFqn = "Adamantium.UI.Core.MarkupExtensions.MarkupExtension";
        foreach (var assembly in _resolver.ResolvedAssemblies)
            foreach (var type in assembly.Types)
            {
                if (type.Name == "MarkupExtension" || !type.InheritsFromMarkupExtension(baseFqn)) continue;
                if (type.Name == localName || type.Name == localName + "Extension") return type;
            }
        return null;
    }

    /// <summary>The markup extension's positional/default argument property (the one marked
    /// <c>[DefaultProperty]</c>, e.g. <c>Binding.Path</c>), or null when the extension has none.</summary>
    public IResolvedProperty? GetDefaultProperty(IResolvedType extensionType) =>
        extensionType.FindPropertyWithAttribute("Adamantium.UI.Core.MarkupExtensions.DefaultPropertyAttribute", out var p) ? p : null;

    private static readonly string[] CommonColors =
    {
        "Transparent", "Black", "White", "Gray", "Silver", "Red", "Green", "Blue", "Yellow",
        "Orange", "Purple", "Pink", "Brown", "Cyan", "Magenta", "Lime", "Navy", "Teal", "Gold",
    };
}
