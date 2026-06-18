namespace Adamantium.UI.Markup.CodeGeneration;

/// <summary>
/// An <see cref="IResolvedAssembly"/> view over several assemblies that share one xmlns URI (e.g. the controls
/// assembly and the core assembly both mapped to "http://adamantium/ui"). Read-only: it unions their types and
/// forwards look-ups to the first part that resolves. The resolver builds one of these when more than one
/// [XmlnsDefinition] maps to the same URI, so consumers keep using a single IResolvedAssembly transparently.
/// </summary>
internal sealed class CompositeResolvedAssembly : IResolvedAssembly
{
    private readonly IReadOnlyList<IResolvedAssembly> _parts;

    public CompositeResolvedAssembly(IReadOnlyList<IResolvedAssembly> parts) => _parts = parts;

    public string Name => string.Join("+", _parts.Select(p => p.Name));

    public IReadOnlyList<IResolvedType> Types
    {
        get
        {
            var seen = new HashSet<string>();
            var list = new List<IResolvedType>();
            foreach (var part in _parts)
            foreach (var type in part.Types)
                if (seen.Add(type.FullName ?? type.Name)) list.Add(type);   // first part wins on a name clash
            return list;
        }
    }

    public IResolvedType GetTypeByShortName(string shortName)
    {
        foreach (var part in _parts)
        {
            var type = part.GetTypeByShortName(shortName);
            if (type != null) return type;
        }
        return null;
    }

    public IEnumerable<IResolvedType> GetTypesByNamespace(string @namespace) =>
        _parts.SelectMany(p => p.GetTypesByNamespace(@namespace));

    public IResolvedType GetTypeByFullName(string fullName)
    {
        foreach (var part in _parts)
        {
            var type = part.GetTypeByFullName(fullName);
            if (type != null) return type;
        }
        return null;
    }

    public void AddType(IResolvedType type) =>
        throw new NotSupportedException("A composite xmlns assembly is read-only.");
}
