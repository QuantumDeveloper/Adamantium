namespace Adamantium.Content.Pipeline;

/// <summary>A single <see cref="BuildCache"/> record for one cooked asset.</summary>
public sealed class CacheEntry
{
    public string SourceHash { get; set; }

    public string ParametersHash { get; set; }

    public string Output { get; set; }
}
