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
}
