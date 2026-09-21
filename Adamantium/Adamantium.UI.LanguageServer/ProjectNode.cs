namespace Adamantium.UI.LanguageServer;

/// <summary>A resolved in-repo project in the source graph: its file, assembly name, directory, and the
/// in-repo projects it directly references.</summary>
public sealed class ProjectNode
{
    public ProjectNode(string csprojPath, string assemblyName, string directory)
    {
        CsprojPath = csprojPath;
        AssemblyName = assemblyName;
        Directory = directory;
    }

    public string CsprojPath { get; }

    public string AssemblyName { get; }

    public string Directory { get; }

    public List<ProjectNode> Dependencies { get; } = [];
}
