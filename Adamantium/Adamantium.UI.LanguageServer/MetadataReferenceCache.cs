using Microsoft.CodeAnalysis;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// Caches <see cref="MetadataReference"/>s for external (non-source) assemblies by path + last-write time, so
/// rebuilds don't re-read every dll's metadata each time. External dlls change only when a dependency is
/// rebuilt, so these stay valid across the frequent source-edit rebuilds.
/// </summary>
public sealed class MetadataReferenceCache
{
    private readonly Dictionary<string, (long Stamp, MetadataReference Reference)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public MetadataReference Get(string path)
    {
        long stamp;
        try { stamp = File.GetLastWriteTimeUtc(path).ToFileTimeUtc(); }
        catch { return null; }

        lock (_cache)
        {
            if (_cache.TryGetValue(path, out var cached) && cached.Stamp == stamp)
                return cached.Reference;
        }

        MetadataReference reference;
        try { reference = MetadataReference.CreateFromFile(path); }
        catch { return null; }

        lock (_cache)
        {
            _cache[path] = (stamp, reference);
        }

        return reference;
    }
}
