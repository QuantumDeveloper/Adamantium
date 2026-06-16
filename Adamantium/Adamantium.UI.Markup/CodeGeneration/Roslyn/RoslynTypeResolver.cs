using Adamantium.UI.Markup.Parsers;
using Microsoft.CodeAnalysis;

namespace Adamantium.UI.Markup.CodeGeneration.Roslyn;

public class RoslynTypeResolver : ITypeResolver
{
    private Compilation _compilation;
    private IDictionary<string, IResolvedAssembly> _resolvedAssembliesMap;
    private List<IResolvedAssembly> _resolvedAssemblies;
    private IDictionary<string, IResolvedAssembly> _resolvedXmlAssemblies;
    private IDictionary<string, IResolvedAssembly> _namespaceToAssembleMap;
    
    public RoslynTypeResolver(Compilation compilation)
    {
        _compilation = compilation;
        _resolvedAssembliesMap = new Dictionary<string, IResolvedAssembly>();
        _resolvedAssemblies = new List<IResolvedAssembly>();
        _resolvedXmlAssemblies = new Dictionary<string, IResolvedAssembly>();
        _namespaceToAssembleMap = new Dictionary<string, IResolvedAssembly>();
    }
    
    public IReadOnlyList<IResolvedAssembly> ResolvedAssemblies => _resolvedAssemblies;

    public IReadOnlyCollection<string> XmlnsDefinitions => _resolvedXmlAssemblies.Keys.ToArray();

    public IResolvedAssembly GetResolvedAssembly(string assemblyName)
    {
        // Fall back to creating the container: the local/compiled assembly is populated lazily and may not be in
        // _resolvedAssemblies yet when a local type (e.g. a Behavior referenced via clr-namespace) is used in markup.
        return _resolvedAssemblies.FirstOrDefault(x => x.Name == assemblyName)
            ?? GetOrCreateTypeContainerForAssembly(assemblyName);
    }

    public IResolvedAssembly GetResolvedAssemblyByXmlDefinition(string xmlDefinition)
    {
        _resolvedXmlAssemblies.TryGetValue(xmlDefinition.Trim('/'), out var result);
        return result;
    }

    public IResolvedType Resolve(string metadataName)
    {
        foreach(var assembly in _resolvedAssemblies)
        {
            var resolvedType = assembly.GetTypeByFullName(metadataName);
            if (resolvedType != null)
            {
                return resolvedType;
            }
        }

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
        
        // The compiled assembly itself is not in ReferencedAssemblySymbols, so resolve local types (e.g. a
        // GameHostBehavior declared in the same project and referenced via clr-namespace) against it explicitly.
        var assemblySymbol = _compilation.Assembly.Name == assemblyName
            ? _compilation.Assembly
            : _compilation.SourceModule.ReferencedAssemblySymbols.FirstOrDefault(q => q.Name == assemblyName);
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
    
    // Find assembly by namespace when assembly is not specified in the markup
    public IResolvedAssembly FindAssemblyByNamespace(string targetNamespace)
    {
        IResolvedAssembly foundAssembly = null;

        foreach(var assembly in _resolvedAssemblies)
        {
            if (DoesAssemblyContainNamespace(assembly, targetNamespace))
            {
                foundAssembly = assembly;
                return assembly;
            }
        }

        // Find in the local assembly first
        var localAssembly = GetLocalAssembly();
        if (DoesAssemblyContainNamespace(localAssembly, targetNamespace))
        {
            foundAssembly = localAssembly;
        }

        // If there is no namespace in a local assembly, try to find in referenced assemblies
        if (foundAssembly == null)
        {
            foreach (var referencedAssembly in GetReferencedAssemblies())
            {
                if (DoesAssemblyContainNamespace(referencedAssembly, targetNamespace))
                {
                    foundAssembly = referencedAssembly;
                    
                    break; 
                }
            }
        }

        if (foundAssembly != null)
        {
            _namespaceToAssembleMap[targetNamespace] = foundAssembly;
        }
        
        return foundAssembly;
    }

    public void RegisterGeneratedType(IResolvedType type)
    {
        var assemblyName = type.AssemblyName;
        if (!_resolvedAssembliesMap.TryGetValue(assemblyName, out var resolvedAssembly))
        {
            var assemblySymbol = _compilation.Assembly;
            resolvedAssembly = GetOrCreateTypeContainerForAssemblyInternal(assemblySymbol);
            _resolvedAssembliesMap[assemblyName] = resolvedAssembly;
        }
        
        resolvedAssembly.AddType(type);
    }

    private IResolvedAssembly GetLocalAssembly()
    {
        return new RoslynResolvedAssembly(_compilation.Assembly);
    }

    private IEnumerable<IResolvedAssembly> GetReferencedAssemblies()
    {
        return _compilation.References
            .Select(r => _compilation.GetAssemblyOrModuleSymbol(r) as IAssemblySymbol)
            .Where(s => s != null)
            .Select(s => new RoslynResolvedAssembly(s));
    }

    private bool DoesAssemblyContainNamespace(IResolvedAssembly assembly, string namespaceName)
    {
        return assembly.Types.Any(t => t.Namespace == namespaceName);
    }
}