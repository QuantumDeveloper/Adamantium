using System;
using System.Collections.Generic;

namespace Adamantium.Content.Pipeline;

/// <summary>
/// Per-asset context handed to importers/processors during a build.
/// </summary>
public sealed class ContentBuildContext
{
    public string ProjectDirectory { get; init; }

    public string OutputDirectory { get; init; }

    public string IntermediateDirectory { get; init; }

    public IReadOnlyDictionary<string, string> Parameters { get; init; } = new Dictionary<string, string>();

    public Action<string> Log { get; init; } = _ => { };

    /// <summary>Puts a file the cooked artifact is incomplete without - a model's texture, say - next to it. First
    /// argument is where to take it from, second is the name to place it under, relative to the artifact. Without
    /// this the artifact references files that are not beside it, and is portable in name only.</summary>
    public Action<string, string> CopyAlongside { get; init; } = (_, _) => { };
}
