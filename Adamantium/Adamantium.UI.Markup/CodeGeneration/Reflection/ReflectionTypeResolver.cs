using System.Reflection;

namespace Adamantium.UI.Markup.CodeGeneration.Reflection;

/// <summary>
/// Reflection-backed <see cref="ITypeResolver"/>: the runtime counterpart of <c>RoslynTypeResolver</c>. It lets
/// <c>DefaultAumlTransformer</c> resolve markup against the real, loaded engine/project assemblies, so the live
/// designer resolves types exactly like the compiler does.
/// </summary>
public class ReflectionTypeResolver : ITypeResolver
{
    private readonly List<Assembly> _assemblies;
    private readonly Dictionary<string, IResolvedAssembly> _resolvedAssembliesMap = new();
    private readonly List<IResolvedAssembly> _resolvedAssemblies = new();
    private readonly Dictionary<string, IResolvedAssembly> _resolvedXmlAssemblies = new();

    public ReflectionTypeResolver(IEnumerable<Assembly> assemblies = null)
    {
        _assemblies = (assemblies ?? AppDomain.CurrentDomain.GetAssemblies()).ToList();
    }

    public IReadOnlyList<IResolvedAssembly> ResolvedAssemblies => _resolvedAssemblies;

    public IReadOnlyCollection<string> XmlnsDefinitions => _resolvedXmlAssemblies.Keys.ToArray();

    public IResolvedAssembly GetResolvedAssembly(string assemblyName) =>
        _resolvedAssemblies.FirstOrDefault(x => x.Name == assemblyName) ?? ResolveAssembly(assemblyName);

    public IResolvedAssembly GetResolvedAssemblyByXmlDefinition(string xmlDefinition)
    {
        _resolvedXmlAssemblies.TryGetValue(xmlDefinition.Trim('/'), out var result);
        return result;
    }

    public IResolvedType Resolve(string metadataName)
    {
        foreach (var assembly in _resolvedAssemblies)
        {
            var resolvedType = assembly.GetTypeByFullName(metadataName);
            if (resolvedType != null) return resolvedType;
        }

        foreach (var asm in _assemblies)
        {
            var type = asm.GetType(metadataName, throwOnError: false);
            if (type != null) return new ReflectionResolvedType(type);
        }
        return null;
    }

    public IResolvedType ResolveByShortName(string metadataName, string assembly = "")
    {
        foreach (var resolvedAssembly in _resolvedAssemblies)
        {
            var type = resolvedAssembly.GetTypeByShortName(metadataName);
            if (type != null) return type;
        }

        foreach (var asm in _assemblies)
        {
            var type = ReflectionResolvedAssembly.SafeGetTypes(asm).FirstOrDefault(t => t.Name == metadataName);
            if (type != null) return new ReflectionResolvedType(type);
        }
        return null;
    }

    public IResolvedAssembly ResolveAssembly(string assembly)
    {
        if (_resolvedAssembliesMap.TryGetValue(assembly, out var resolved)) return resolved;
        return GetOrCreateTypeContainerForAssembly(assembly);
    }

    public List<IResolvedAssembly> ScanXmlnsAttributes()
    {
        var result = new List<IResolvedAssembly>();
        const string attrName = "Adamantium.UI.Core.Markup.XmlnsDefinitionAttribute";

        foreach (var asm in _assemblies)
        {
            // Read the attribute as metadata (CustomAttributeData) rather than instantiating it: cheaper and
            // reflection-only-safe, and the raw "clr-namespace:...;assembly=..." string is parsed here.
            IList<CustomAttributeData> datas;
            try { datas = CustomAttributeData.GetCustomAttributes(asm); }
            catch { continue; }

            foreach (var data in datas)
            {
                if (data.AttributeType.FullName != attrName || data.ConstructorArguments.Count < 2) continue;

                var xmlNs = data.ConstructorArguments[0].Value as string;
                var clrNsRaw = data.ConstructorArguments[1].Value as string;
                if (xmlNs == null || clrNsRaw == null) continue;

                var (_, asmName) = ParseClrNamespace(clrNsRaw);
                var container = GetOrCreateTypeContainerForAssembly(asmName, xmlNs);
                if (container != null) result.Add(container);
            }
        }
        return result;
    }

    // clr-namespace:Some.Ns;assembly=Some.Asm  (also tolerates an 'assembly:' separator).
    private static (string ClrNamespace, string Assembly) ParseClrNamespace(string value)
    {
        string clrNs = string.Empty, asm = string.Empty;
        foreach (var raw in value.Split(';'))
        {
            var p = raw.Trim();
            if (p.StartsWith("clr-namespace:")) clrNs = p.Substring("clr-namespace:".Length);
            else if (p.StartsWith("assembly=")) asm = p.Substring("assembly=".Length);
            else if (p.StartsWith("assembly:")) asm = p.Substring("assembly:".Length);
        }
        return (clrNs, asm);
    }

    public IResolvedAssembly GetOrCreateTypeContainerForAssembly(string assemblyName, string xmlNamespace = "")
    {
        if (_resolvedAssembliesMap.TryGetValue(assemblyName, out var resolved))
        {
            EnsureXmlDefinitionAssemblyAdded(resolved, xmlNamespace);
            return resolved;
        }

        var asm = _assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName);
        if (asm == null) return null;

        var container = new ReflectionResolvedAssembly(asm);
        _resolvedAssembliesMap[assemblyName] = container;
        _resolvedAssemblies.Add(container);
        EnsureXmlDefinitionAssemblyAdded(container, xmlNamespace);
        return container;
    }

    public IResolvedAssembly FindAssemblyByNamespace(string targetNamespace)
    {
        foreach (var assembly in _resolvedAssemblies)
            if (assembly.Types.Any(t => t.Namespace == targetNamespace))
                return assembly;

        foreach (var asm in _assemblies)
        {
            if (ReflectionResolvedAssembly.SafeGetTypes(asm).Any(t => t.Namespace == targetNamespace))
                return GetOrCreateTypeContainerForAssembly(asm.GetName().Name);
        }
        return null;
    }

    public void RegisterGeneratedType(IResolvedType type) { /* no generated types at runtime */ }

    private void EnsureXmlDefinitionAssemblyAdded(IResolvedAssembly assembly, string xmlDefinition)
    {
        if (!string.IsNullOrEmpty(xmlDefinition))
        {
            var key = xmlDefinition.Trim('/');
            if (!_resolvedXmlAssemblies.ContainsKey(key))
                _resolvedXmlAssemblies[key] = assembly;
        }
    }
}
