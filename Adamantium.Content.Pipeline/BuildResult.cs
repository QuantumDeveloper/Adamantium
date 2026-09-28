namespace Adamantium.Content.Pipeline;

/// <summary>Summary of a content build.</summary>
public sealed class BuildResult
{
    public int Cooked { get; set; }

    public int Skipped { get; set; }

    public int Failed { get; set; }

    public override string ToString() => $"cooked: {Cooked}, up-to-date: {Skipped}, failed: {Failed}";
}
