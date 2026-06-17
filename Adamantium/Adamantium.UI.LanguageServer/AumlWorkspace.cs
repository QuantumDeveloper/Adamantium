using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// Resolves and caches an <see cref="AumlTypeModel"/> per project: a .auml file's completion
/// uses the types of the project that contains it (its build output) rather than a fixed
/// assembly set. The cached model is dropped automatically when the project's build output
/// changes (a <see cref="FileSystemWatcher"/> on the output dir), so newly added types/properties
/// show up after a rebuild without restarting the language server. A project that has not been
/// built yet is not cached, so completion enables itself once the first build appears.
/// </summary>
public sealed class AumlWorkspace : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, AumlTypeModel?> _byProject = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Timer> _debounce = new(StringComparer.OrdinalIgnoreCase);

    // A build writes/copies many dlls in a burst; collapse the burst into one refresh once it goes quiet.
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(800);

    /// <summary>Raised (off the message-loop thread) after a project's build output changed and its cached type
    /// model was dropped, so the server can re-validate open documents and stale diagnostics clear without an edit.</summary>
    public event Action? ModelsChanged;

    /// <summary>Type model for the project that contains <paramref name="filePath"/>, or null.</summary>
    public AumlTypeModel? GetModelForFile(string filePath)
    {
        var project = FindProjectFile(filePath);
        if (project is null)
        {
            Console.Error.WriteLine($"[auml] no .csproj found above {filePath}");
            return null;
        }

        lock (_gate)
        {
            if (_byProject.TryGetValue(project, out var cached))
                return cached;

            var binDir = FindProjectBinDir(project);
            if (binDir is null)
            {
                // Deliberately NOT cached: the next request retries, so completion enables itself once the
                // project is built — no language-server restart needed.
                Console.Error.WriteLine($"[auml] no build output for {Path.GetFileName(project)} — build it once; completion enables itself after the build (no restart needed)");
                return null;
            }

            Console.Error.WriteLine($"[auml] {Path.GetFileName(project)} -> types from {binDir}");
            var model = BuildFromBin(binDir);
            _byProject[project] = model;
            WatchBin(project, binDir);
            return model;
        }
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

    // Watch the resolved output dir: when a rebuild copies new dlls there, drop the cached model so the next
    // request rebuilds it from the fresh assemblies. One watcher per project, kept for the session.
    private void WatchBin(string project, string binDir)
    {
        if (_watchers.ContainsKey(project)) return;
        try
        {
            var watcher = new FileSystemWatcher(binDir, "*.dll")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
                IncludeSubdirectories = false,
            };
            FileSystemEventHandler onChange = (_, _) => ScheduleInvalidate(project);
            watcher.Changed += onChange;
            watcher.Created += onChange;
            watcher.Deleted += onChange;
            watcher.Renamed += (_, _) => ScheduleInvalidate(project);
            watcher.EnableRaisingEvents = true;
            _watchers[project] = watcher;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[auml] could not watch {binDir} ({ex.Message}); completion won't auto-refresh for {Path.GetFileName(project)}");
        }
    }

    private void ScheduleInvalidate(string project)
    {
        lock (_gate)
        {
            if (_debounce.TryGetValue(project, out var timer))
                timer.Change(DebounceDelay, Timeout.InfiniteTimeSpan);
            else
                _debounce[project] = new Timer(_ => Invalidate(project), null, DebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private void Invalidate(string project)
    {
        lock (_gate)
        {
            _byProject.Remove(project);
            if (_debounce.Remove(project, out var timer)) timer.Dispose();
        }
        Console.Error.WriteLine($"[auml] build output changed — type model refreshed for {Path.GetFileName(project)}");
        ModelsChanged?.Invoke();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var watcher in _watchers.Values) watcher.Dispose();
            _watchers.Clear();
            foreach (var timer in _debounce.Values) timer.Dispose();
            _debounce.Clear();
        }
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
