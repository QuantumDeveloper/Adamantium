using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// Builds a Roslyn compilation for a project where every in-repo dependency project is compiled from source
/// and added as a <see cref="CompilationReference"/> — preserving each assembly's identity (so
/// <c>[XmlnsDefinition(..., assembly=...)]</c> still resolves) while making its types/properties live from
/// source with no build. Everything outside the repo (NuGet, framework, cross-repo bindings) is referenced as
/// the dll already sitting in <c>binDir</c>. Incremental: only changed source files are re-parsed (via the
/// shared <see cref="SyntaxTreeCache"/>), so rebuilding after a save is cheap.
/// </summary>
public static class SourceProjectGraph
{
    // allowUnsafe so the engine's unsafe members (Vulkan interop) compile; without it their declarations fail
    // and the types that contain them resolve incompletely.
    private static readonly CSharpCompilationOptions Options = new(OutputKind.DynamicallyLinkedLibrary, allowUnsafe: true);

    // The engine projects build with <ImplicitUsings>enable</ImplicitUsings>; the SDK normally emits these as a
    // generated obj/**/GlobalUsings.g.cs (excluded here with the rest of obj). Synthesizing them instead keeps
    // the source compile working with System types unqualified — without them even "Attribute" / "Double" don't
    // resolve, attributes don't bind, and nothing materializes. This is the default Microsoft.NET.Sdk set.
    private static readonly SyntaxTree ImplicitUsingsTree = CSharpSyntaxTree.ParseText(
        """
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.IO;
        global using global::System.Linq;
        global using global::System.Net.Http;
        global using global::System.Threading;
        global using global::System.Threading.Tasks;
        """);

    /// <summary>
    /// Builds the root project's compilation. <paramref name="repoRoot"/> (the directory holding the shared
    /// <c>artifacts</c> output) is the boundary: project references under it are compiled from source, the rest
    /// stay as dlls. Returns the root compilation plus the repo root to watch for <c>*.cs</c> changes.
    /// </summary>
    public static (Compilation Root, string RepoRoot, IReadOnlyList<(string XmlNamespace, string ClrSpec)> XmlnsMappings) Build(
        string rootCsproj, string binDir, SyntaxTreeCache syntaxCache, MetadataReferenceCache metadataCache)
    {
        var repoRoot = FindRepoRoot(binDir) ?? Path.GetDirectoryName(Path.GetFullPath(rootCsproj));

        var nodes = new Dictionary<string, ProjectNode>(StringComparer.OrdinalIgnoreCase);
        var root = ResolveNode(Path.GetFullPath(rootCsproj), repoRoot, nodes);

        // External references: every dll in binDir whose assembly isn't one of our in-repo projects (those are
        // provided live from source), plus the runtime assemblies. Shared by every sub-compilation.
        var inRepoDlls = new HashSet<string>(
            nodes.Values.Select(n => n.AssemblyName + ".dll"), StringComparer.OrdinalIgnoreCase);
        var externalRefs = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Framework references: the running runtime's trusted platform assemblies — the canonical managed-only
        // reference set. Enumerating every *.dll in the runtime directory instead drags in native images
        // (msquic, hostpolicy, *.Native.dll, …) that carry no managed metadata (CS0009) and a facade soup that
        // leaves even System.Object/Attribute unbound — so attribute constructor arguments never materialize.
        var tpa = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dll in tpa)
        {
            var name = Path.GetFileName(dll);
            if (inRepoDlls.Contains(name) || !seen.Add(name)) continue;
            if (metadataCache.Get(dll) is { } reference) externalRefs.Add(reference);
        }

        // The project's third-party / cross-repo dependencies this process didn't itself load (e.g. the Vulkan
        // bindings, NuGet packages), taken from the real build output dir.
        foreach (var dll in Directory.GetFiles(binDir, "*.dll"))
        {
            var name = Path.GetFileName(dll);
            if (inRepoDlls.Contains(name) || !seen.Add(name)) continue;
            if (metadataCache.Get(dll) is { } reference) externalRefs.Add(reference);
        }

        // Build each project's compilation deps-first, referencing the CompilationReferences of its transitive
        // in-repo dependencies so cross-project inheritance resolves live too.
        var compilations = new Dictionary<string, Compilation>(StringComparer.OrdinalIgnoreCase);
        var xmlnsMappings = new List<(string XmlNamespace, string ClrSpec)>();
        foreach (var node in TopologicalOrder(root))
        {
            var trees = EnumerateSources(node.Directory)
                .Select(syntaxCache.Get)
                .Where(tree => tree is not null)
                .ToList();
            trees.Add(ImplicitUsingsTree);

            var references = new List<MetadataReference>(externalRefs);
            foreach (var dependency in TransitiveDependencies(node))
                references.Add(compilations[dependency.CsprojPath].ToMetadataReference());

            var compilation = CSharpCompilation.Create(node.AssemblyName, trees, references, Options);
            compilations[node.CsprojPath] = compilation;
            CollectXmlnsMappings(compilation, xmlnsMappings);
        }

        return (compilations[root.CsprojPath], repoRoot, xmlnsMappings);
    }

    // [XmlnsDefinition] attributes are read from each project's own compilation, where the constructor
    // arguments are materialized — surfaced through a CompilationReference into the root they are not — then
    // injected into the resolver (see RoslynTypeResolver.AddXmlnsMapping).
    private static void CollectXmlnsMappings(Compilation compilation, List<(string XmlNamespace, string ClrSpec)> into)
    {
        const string xmlnsAttrFqn = "Adamantium.UI.Core.Markup.XmlnsDefinitionAttribute";
        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (attr.AttributeClass?.ToDisplayString() != xmlnsAttrFqn) continue;
            if (attr.ConstructorArguments.Length < 2) continue;
            var xmlNamespace = attr.ConstructorArguments[0].Value?.ToString();
            var clrSpec = attr.ConstructorArguments[1].Value?.ToString();
            if (xmlNamespace != null && clrSpec != null) into.Add((xmlNamespace, clrSpec));
        }
    }

    private static ProjectNode ResolveNode(string csprojPath, string repoRoot, Dictionary<string, ProjectNode> nodes)
    {
        if (nodes.TryGetValue(csprojPath, out var existing)) return existing;

        var directory = Path.GetDirectoryName(csprojPath)!;
        XDocument doc;
        try { doc = XDocument.Load(csprojPath); }
        catch { doc = null; }

        var assemblyName = doc?.Descendants().FirstOrDefault(e => e.Name.LocalName == "AssemblyName")?.Value?.Trim();
        if (string.IsNullOrEmpty(assemblyName)) assemblyName = Path.GetFileNameWithoutExtension(csprojPath);

        var node = new ProjectNode(csprojPath, assemblyName, directory);
        nodes[csprojPath] = node;   // add before recursing so a diamond doesn't re-resolve

        var references = doc?.Descendants().Where(e => e.Name.LocalName == "ProjectReference")
            .Select(e => e.Attribute("Include")?.Value)
            .Where(v => !string.IsNullOrEmpty(v)) ?? [];

        foreach (var include in references)
        {
            var depPath = Path.GetFullPath(Path.Combine(directory, include.Replace('\\', '/')));
            // Only compile in-repo projects from source; cross-repo references (e.g. the Vulkan bindings) keep
            // using their dll from binDir.
            if (File.Exists(depPath) && IsUnder(depPath, repoRoot))
                node.Dependencies.Add(ResolveNode(depPath, repoRoot, nodes));
        }

        return node;
    }

    private static IEnumerable<ProjectNode> TopologicalOrder(ProjectNode root)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = new List<ProjectNode>();

        void Visit(ProjectNode node)
        {
            if (!visited.Add(node.CsprojPath)) return;
            foreach (var dependency in node.Dependencies) Visit(dependency);
            order.Add(node);   // post-order: dependencies precede the node that needs them
        }

        Visit(root);
        return order;
    }

    private static IEnumerable<ProjectNode> TransitiveDependencies(ProjectNode node)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<ProjectNode>();

        void Collect(ProjectNode current)
        {
            foreach (var dependency in current.Dependencies)
                if (seen.Add(dependency.CsprojPath))
                {
                    result.Add(dependency);
                    Collect(dependency);
                }
        }

        Collect(node);
        return result;
    }

    private static IEnumerable<string> EnumerateSources(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
        {
            var relative = file.Substring(directory.Length).TrimStart('/', '\\').Replace('\\', '/');
            if (!relative.StartsWith("obj/", StringComparison.OrdinalIgnoreCase) &&
                !relative.StartsWith("bin/", StringComparison.OrdinalIgnoreCase))
                yield return file;
        }
    }

    // The repo/solution directory is the parent of the shared "artifacts" output folder that binDir lives under.
    private static string FindRepoRoot(string binDir)
    {
        for (var dir = new DirectoryInfo(binDir); dir is not null; dir = dir.Parent)
            if (dir.Name.Equals("artifacts", StringComparison.OrdinalIgnoreCase))
                return dir.Parent?.FullName;
        return null;
    }

    private static bool IsUnder(string path, string root) =>
        path.StartsWith(root.TrimEnd('/', '\\') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
