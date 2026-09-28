using System.Collections.Generic;

namespace Adamantium.Content.Pipeline;

/// <summary>A single asset entry in a <see cref="ContentManifest"/>.</summary>
public sealed class ContentAsset
{
    /// <summary>Source file, relative to the project directory (forward slashes).</summary>
    public string Source { get; set; }

    /// <summary>Importer name (e.g. "Model"). When empty, resolved by source extension.</summary>
    public string Importer { get; set; }

    /// <summary>Optional processor name. When empty, no processing step is run.</summary>
    public string Processor { get; set; }

    /// <summary>Logical load name. When empty, defaults to <see cref="Source"/> (extension kept).</summary>
    public string LogicalName { get; set; }

    /// <summary>Per-asset import settings (none = importer/processor defaults).</summary>
    public Dictionary<string, string> Parameters { get; set; } = new();
}
