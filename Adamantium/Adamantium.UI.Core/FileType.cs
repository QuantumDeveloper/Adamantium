using System;
using System.Collections.Generic;

namespace Adamantium.UI.Core;

/// <summary>One kind of file a dialog offers - a name a person reads, and the extensions that belong to it.
/// <para>Extensions are bare, WITHOUT the dot or a wildcard: every platform spells the pattern differently (Windows
/// wants <c>*.csv</c>, a GTK filter wants the same, macOS wants the type), and a request that carried one platform's
/// spelling could not be answered by another.</para></summary>
public sealed class FileType
{
    /// <param name="name">What the user reads, e.g. "Comma-separated values".</param>
    /// <param name="extensions">One or more bare extensions, e.g. "csv".</param>
    public FileType(string name, params string[] extensions)
    {
        Name = name;
        Extensions = extensions ?? Array.Empty<string>();
    }

    public string Name { get; }

    public IReadOnlyList<string> Extensions { get; }
}
