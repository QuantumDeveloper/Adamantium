namespace Adamantium.UI.Markup.CodeGeneration;

public interface IResolvedAssembly
{
    string Name { get; }
    
    IReadOnlyList<IResolvedType> Types { get; }
    
    IResolvedType GetTypeByShortName(string shortName);
    
    IEnumerable<IResolvedType> GetTypesByNamespace(string @namespace);

    IResolvedType GetTypeByFullName(string fullName);
    
    void AddType(IResolvedType type);
}