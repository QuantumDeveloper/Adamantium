namespace Adamantium.UI.Markup.CodeGeneration;

public interface ITypeResolver
{
    IReadOnlyList<IResolvedAssembly> ResolvedAssemblies { get; }

    IResolvedAssembly GetResolvedAssembly(string assemblyName);

    IResolvedAssembly GetResolvedAssemblyByXmlDefinition(string xmlDefinition);
    
    IResolvedType Resolve(string metadataName);
    
    IResolvedType ResolveByShortName(string metadataName, string assembly = "");
    
    IResolvedAssembly ResolveAssembly(string assembly);

    List<IResolvedAssembly> ScanXmlnsAttributes();

    IResolvedAssembly GetOrCreateTypeContainerForAssembly(string assemblyName, string xmlNamespace = "");
    
    IResolvedAssembly FindAssemblyByNamespace(string targetNamespace);

    void RegisterGeneratedType(IResolvedType type);
}