using Adamantium.UI.Markup.CodeGeneration;
using Adamantium.UI.Markup.CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// AUML type model: builds a Roslyn compilation from the target project's referenced
/// assemblies and reuses the engine's own <see cref="RoslynTypeResolver"/>, so completion
/// sees exactly the types the AUML source generator sees.
/// </summary>
public sealed class AumlTypeModel
{
    private readonly ITypeResolver _resolver;

    private AumlTypeModel(ITypeResolver resolver) => _resolver = resolver;

    public static AumlTypeModel Build(IEnumerable<string> assemblyPaths)
    {
        var references = new List<MetadataReference>();
        foreach (var path in assemblyPaths)
        {
            if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) continue;
            try { references.Add(MetadataReference.CreateFromFile(path)); }
            catch { /* native or otherwise non-managed dll — skip */ }
        }

        var compilation = CSharpCompilation.Create(
            "AumlTooling",
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var resolver = new RoslynTypeResolver(compilation);
        resolver.ScanXmlnsAttributes();
        return new AumlTypeModel(resolver);
    }

    /// <summary>Element types available under an xmlns (e.g. "http://adamantium/ui").</summary>
    public IReadOnlyList<IResolvedType> GetElements(string xmlns)
    {
        var assembly = _resolver.GetResolvedAssemblyByXmlDefinition(xmlns);
        return assembly?.Types ?? (IReadOnlyList<IResolvedType>)Array.Empty<IResolvedType>();
    }

    public IResolvedType? GetElement(string xmlns, string name) =>
        GetElements(xmlns).FirstOrDefault(t => t.Name == name);

    /// <summary>Settable properties of an element type.</summary>
    public IReadOnlyList<IResolvedProperty> GetProperties(IResolvedType element) =>
        element.GetAllProperties();

    /// <summary>Enum member names if the type is an enum, otherwise empty.</summary>
    public IEnumerable<string> GetEnumValues(IResolvedType type) =>
        type.TypeKind == ResolvedTypeKind.Enum
            ? type.Members
                .Where(m => m.MemberKind == ResolvedMemberKind.Field && m.Name != "value__")
                .Select(m => m.Name)
            : Enumerable.Empty<string>();
}
