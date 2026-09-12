using System.Collections.Generic;

namespace Adamantium.UI.Core;

/// <summary>What to ask the user when saving. Everything here is a SUGGESTION - the dialog is the user's, and they may
/// answer with any of it changed.</summary>
public sealed class SaveFileRequest
{
    /// <summary>The dialog's caption. Left unset, the platform uses its own wording for saving.</summary>
    public string Title { get; init; }

    /// <summary>The name to start with, extension included - what the user overtypes rather than composes.</summary>
    public string FileName { get; init; }

    /// <summary>Extension to append when the user types a name without one, WITHOUT the dot.</summary>
    public string DefaultExtension { get; init; }

    /// <summary>The kinds of file on offer, first one selected. Also what the dialog filters the folder by, so a user
    /// saving a table is not shown every file already in it.</summary>
    public IReadOnlyList<FileType> FileTypes { get; init; }
}
