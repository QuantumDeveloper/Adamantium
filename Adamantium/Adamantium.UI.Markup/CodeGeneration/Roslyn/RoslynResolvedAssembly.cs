using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Markup.CodeGeneration.Roslyn;

public class RoslynResolvedAssembly : IResolvedAssembly
{
    private readonly IAssemblySymbol _assemblySymbol;
    private readonly List<IResolvedType> _types;

    public RoslynResolvedAssembly(IAssemblySymbol assemblySymbol)
    {
        _assemblySymbol = assemblySymbol;
        Name = _assemblySymbol.Name;
        
        var currentNamespace = assemblySymbol.GlobalNamespace.GetNamespaceMembers().FirstOrDefault();

        _types = new List<IResolvedType>();

        var nsStack = new Stack<INamespaceSymbol>();
        nsStack.Push(currentNamespace);
        while (nsStack.Count > 0)
        {
            var nsSymbol = nsStack.Pop();
            var members = nsSymbol.GetTypeMembers().Select(x=>new RoslynResolvedType(x)).ToList();
            _types.AddRange(members);

            foreach (var nsMember in nsSymbol.GetNamespaceMembers())
            {
                nsStack.Push(nsMember);
            }
        }
    }
    
    public string Name { get; }

    // All available types in assembly
    public IReadOnlyList<IResolvedType> Types => _types;
    public IResolvedType GetTypeByShortName(string shortName)
    {
        return Types.FirstOrDefault(x=>x.Name == shortName);
    }

    public IEnumerable<IResolvedType> GetTypesByNamespace(string @namespace)
    {
        return _types.Where(t => t.Namespace == @namespace);
    }

    public IResolvedType GetTypeByFullName(string fullName)
    {
        return _types.FirstOrDefault(x => x.FullName == fullName);
    }
}