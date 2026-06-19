using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Adamantium.Content.Pipeline;

/// <summary>
/// Incremental build cache (the <c>.acache</c> file). Records, per asset source, a content hash of the
/// source plus a hash of its import parameters and the cooked output path, so the builder can skip assets
/// whose source/params are unchanged and whose output still exists.
/// </summary>
public sealed class BuildCache
{
    public Dictionary<string, CacheEntry> Entries { get; set; } = new();

    public static BuildCache Load(string path)
    {
        if (!File.Exists(path))
        {
            return new BuildCache();
        }

        return JsonSerializer.Deserialize<BuildCache>(File.ReadAllText(path)) ?? new BuildCache();
    }

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    public bool IsStale(string sourceKey, string sourceFullPath, IReadOnlyDictionary<string, string> parameters, string outputFullPath)
    {
        if (!Entries.TryGetValue(sourceKey, out var entry))
        {
            return true;
        }

        if (!File.Exists(outputFullPath))
        {
            return true;
        }

        return entry.SourceHash != HashFile(sourceFullPath) || entry.ParametersHash != HashParameters(parameters);
    }

    public void Update(string sourceKey, string sourceFullPath, IReadOnlyDictionary<string, string> parameters, string outputRelativePath)
    {
        Entries[sourceKey] = new CacheEntry
        {
            SourceHash = HashFile(sourceFullPath),
            ParametersHash = HashParameters(parameters),
            Output = outputRelativePath
        };
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static string HashParameters(IReadOnlyDictionary<string, string> parameters)
    {
        if (parameters == null || parameters.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var pair in parameters.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
        }

        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString())));
    }
}
