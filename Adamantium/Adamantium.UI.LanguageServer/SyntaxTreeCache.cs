using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Adamantium.UI.LanguageServer;

/// <summary>
/// Caches parsed C# syntax trees by file path + last-write time, so an incremental rebuild re-parses only the
/// files that actually changed. Shared across model rebuilds for the session — this is what keeps a save fast
/// even when the whole engine source is in the model.
/// </summary>
public sealed class SyntaxTreeCache
{
    private readonly Dictionary<string, (long Stamp, SyntaxTree Tree)> _cache = new(StringComparer.OrdinalIgnoreCase);

    public SyntaxTree Get(string path)
    {
        long stamp;
        try { stamp = File.GetLastWriteTimeUtc(path).ToFileTimeUtc(); }
        catch { return null; }

        lock (_cache)
        {
            if (_cache.TryGetValue(path, out var cached) && cached.Stamp == stamp)
                return cached.Tree;
        }

        SyntaxTree tree;
        try { tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path); }
        catch { return null; }

        lock (_cache)
        {
            _cache[path] = (stamp, tree);
        }

        return tree;
    }
}
