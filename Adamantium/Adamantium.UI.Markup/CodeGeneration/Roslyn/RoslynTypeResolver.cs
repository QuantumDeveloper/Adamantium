using Adamantium.UI.Markup.Parsers;
using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Markup.CodeGeneration.Roslyn;

public class RoslynTypeResolver : ITypeResolver
{
    private Compilation _compilation;
    private IDictionary<string, IResolvedAssembly> _resolvedAssembliesMap;
    private List<IResolvedAssembly> _resolvedAssemblies;
    private IDictionary<string, IResolvedAssembly> _resolvedXmlAssemblies;
    
    public RoslynTypeResolver(Compilation compilation)
    {
        _compilation = compilation;
        _resolvedAssembliesMap = new Dictionary<string, IResolvedAssembly>();
        _resolvedAssemblies = new List<IResolvedAssembly>();
        _resolvedXmlAssemblies = new Dictionary<string, IResolvedAssembly>();
    }
    
    public IReadOnlyList<IResolvedAssembly> ResolvedAssemblies => _resolvedAssemblies;

    public IResolvedAssembly GetResolvedAssembly(string assemblyName)
    {
        return _resolvedAssemblies.FirstOrDefault(x => x.Name == assemblyName);
    }

    public IResolvedAssembly GetResolvedAssemblyByXmlDefinition(string xmlDefinition)
    {
        _resolvedXmlAssemblies.TryGetValue(xmlDefinition, out var result);
        return result;
    }

    public IResolvedType Resolve(string metadataName)
    {
        var symbol = _compilation.GetTypeByMetadataName(metadataName);
        return symbol != null ? new RoslynResolvedType(symbol) :  null;
    }

    public IResolvedType ResolveByShortName(string metadataName, string assembly = "")
    {
        if (string.IsNullOrEmpty(metadataName))
        {
            var assemblySymbol = GetOrCreateTypeContainerForAssembly(assembly);
            var type = assemblySymbol.GetTypeByShortName(metadataName);
            return type;
        }
        
        foreach (var resolvedAssembly in _resolvedAssemblies)
        {
            var type = resolvedAssembly.GetTypeByShortName(metadataName);
            if (type != null)
            {
                return type;
            }
        }

        foreach(var assemblySymbol in _compilation.SourceModule.ReferencedAssemblySymbols)
        {
            var typeName = assemblySymbol.TypeNames.FirstOrDefault(x => x == metadataName);
            if (typeName != null)
            {
                var resolvedAssembly = GetOrCreateTypeContainerForAssemblyInternal(assemblySymbol);
                return resolvedAssembly.GetTypeByShortName(typeName);
            }
        }

        return null;
    }

    public IResolvedAssembly ResolveAssembly(string assembly)
    {
        if (_resolvedAssembliesMap.TryGetValue(assembly, out var resolvedAssembly))
        {
            return resolvedAssembly;
        }

        return GetOrCreateTypeContainerForAssembly(assembly);
    }

    public List<IResolvedAssembly> ScanXmlnsAttributes()
    {
        var xmlnsAttrSymbol = _compilation.GetTypeByMetadataName("Adamantium.UI.Core.Markup.XmlnsDefinitionAttribute");
            
        if (xmlnsAttrSymbol == null)
            return null;

        var result = new List<IResolvedAssembly>();
        foreach (var asm in _compilation.References)
        {
            var symbol = _compilation.GetAssemblyOrModuleSymbol(asm) as IAssemblySymbol;
            if (symbol is null) continue;

            foreach (var attr in symbol.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, xmlnsAttrSymbol))
                    continue;

                if (attr.ConstructorArguments.Length < 2)
                    continue;

                var xmlNs = attr.ConstructorArguments[0].Value?.ToString();
                var clrNs = attr.ConstructorArguments[1].Value?.ToString();
                if (xmlNs != null && clrNs != null)
                {
                    var clr = clrNs.ParseXmlNamespace();
                    var typeContainer = GetOrCreateTypeContainerForAssembly(clr.Assembly, xmlNs);
                    result.Add(typeContainer);
                }
            }
        }

        return result;
    }

    public IResolvedAssembly GetOrCreateTypeContainerForAssembly(string assemblyName, string xmlNamespace = "")
    {
        if (_resolvedAssembliesMap.TryGetValue(assemblyName, out var resolvedAssembly))
        {
            EnsureXmlDefinitionAssemblyAdded(resolvedAssembly, xmlNamespace);
            return resolvedAssembly;
        }
        
        var assemblySymbol = _compilation.SourceModule.ReferencedAssemblySymbols.FirstOrDefault(q => q.Name == assemblyName);
        return GetOrCreateTypeContainerForAssemblyInternal(assemblySymbol, xmlNamespace);
    }

    private IResolvedAssembly GetOrCreateTypeContainerForAssemblyInternal(IAssemblySymbol assembly, string xmlNamespace = "")
    {
        if (assembly != null)
        {
            var container = new RoslynResolvedAssembly(assembly);
            _resolvedAssembliesMap[assembly.Name] = container;
            _resolvedAssemblies.Add(container);
            EnsureXmlDefinitionAssemblyAdded(container, xmlNamespace);
            return container;
        }

        return null;
    }

    private void EnsureXmlDefinitionAssemblyAdded(IResolvedAssembly assembly, string xmlDefinition)
    {
        if (!string.IsNullOrEmpty(xmlDefinition) && !_resolvedXmlAssemblies.ContainsKey(xmlDefinition))
        {
            _resolvedXmlAssemblies[xmlDefinition] = assembly;
        }
    }
}