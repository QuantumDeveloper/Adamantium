using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// Resolves and caches an <see cref="AumlTypeModel"/> per project: a .auml file's completion
/// uses the types of the project that contains it (its build output) rather than a fixed
/// assembly set. The model is built once per project and cached for the session.
/// </summary>
public sealed class AumlWorkspace
{
    private readonly Dictionary<string, AumlTypeModel?> _byProject = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Type model for the project that contains <paramref name="filePath"/>, or null.</summary>
    public AumlTypeModel? GetModelForFile(string filePath)
    {
        var project = FindProjectFile(filePath);
        if (project is null)
        {
            Console.Error.WriteLine($"[auml] no .csproj found above {filePath}");
            return null;
        }

        if (_byProject.TryGetValue(project, out var cached))
            return cached;

        var binDir = FindProjectBinDir(project);
        AumlTypeModel? model = null;
        if (binDir is null)
            Console.Error.WriteLine($"[auml] no build output for {Path.GetFileName(project)} — build it once; completion disabled until then");
        else
        {
            Console.Error.WriteLine($"[auml] {Path.GetFileName(project)} -> types from {binDir}");
            model = BuildFromBin(binDir);
        }

        _byProject[project] = model;
        return model;
    }

    /// <summary>Builds a type model from every managed dll in <paramref name="binDir"/> plus the runtime.</summary>
    public static AumlTypeModel BuildFromBin(string binDir)
    {
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dll in Directory.GetFiles(binDir, "*.dll"))
            byName[Path.GetFileName(dll)] = dll;
        foreach (var dll in Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
            byName.TryAdd(Path.GetFileName(dll), dll);
        return AumlTypeModel.Build(byName.Values);
    }

    private static string? FindProjectFile(string filePath)
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(filePath))!);
        for (; dir is not null; dir = dir.Parent)
        {
            var csproj = dir.GetFiles("*.csproj").FirstOrDefault();
            if (csproj is not null) return csproj.FullName;
        }
        return null;
    }

    private static string? FindProjectBinDir(string csprojPath)
    {
        var projectDir = Path.GetDirectoryName(csprojPath)!;

        // This engine redirects output via <BaseOutputPath> (e.g. ..\..\output\Name\bin); honour it,
        // otherwise fall back to the conventional <projectDir>\bin.
        var baseOutput = ReadBaseOutputPath(csprojPath);
        var binBase = baseOutput is not null
            ? Path.GetFullPath(Path.Combine(projectDir, baseOutput))
            : Path.Combine(projectDir, "bin");
        if (!Directory.Exists(binBase)) return null;

        // The real output leaf is the directory holding the most dlls (the project + its closure),
        // regardless of platform/config/tfm nesting or the assembly name.
        return Directory.EnumerateDirectories(binBase, "*", SearchOption.AllDirectories)
            .Prepend(binBase)
            .Where(d => Directory.EnumerateFiles(d, "*.dll").Any())
            .OrderByDescending(d => Directory.GetFiles(d, "*.dll").Length)
            .ThenByDescending(Directory.GetLastWriteTimeUtc)
            .FirstOrDefault();
    }

    private static string? ReadBaseOutputPath(string csprojPath)
    {
        try
        {
            return XDocument.Load(csprojPath).Descendants("BaseOutputPath").FirstOrDefault()?.Value?.Trim();
        }
        catch
        {
            return null;
        }
    }
}
