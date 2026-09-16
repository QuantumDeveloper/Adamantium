using System.Collections.Generic;

namespace Adamantium.UI.Core;

/// <summary>What to ask the user when opening. No suggested name: a file being opened is one that already exists, and
/// the only thing worth suggesting about it is where to look.</summary>
public sealed class OpenFileRequest
{
    /// <summary>The dialog's caption. Left unset, the platform uses its own wording for opening.</summary>
    public string Title { get; init; }

    /// <summary>The kinds of file on offer, first one selected.</summary>
    public IReadOnlyList<FileType> FileTypes { get; init; }
}
