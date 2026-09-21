using System;
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

    /// <summary>WHAT THIS DIALOG IS FOR, as a name of the application's own choosing - "canvas.picture",
    /// "table.export". The platform remembers a dialog's size, its place on screen and the folder last used under this
    /// name, and gives them back the next time the same one is opened.
    /// <para>Left unset, every dialog in the application shares one memory: the shape the last one was dragged into is
    /// the shape the next one comes up in, however different a question it is asking.</para>
    /// <para>A NAME rather than a platform's own identifier, because the platforms disagree about what they keep - one
    /// wants a GUID, another a folder path, a third nothing at all - and an application should not have to know which
    /// it is talking to.</para></summary>
    public string Key { get; init; }

    /// <summary>The window this is being asked ON BEHALF OF - the dialog is placed over it and blocks it while it is
    /// open. Left unset, the platform finds the window in front, which is right in an application with one.
    /// <para>What this actually decides is WHICH SCREEN the dialog appears on: a dialog belongs to its owner and opens
    /// where the owner is, and one with no owner opens on the main screen - which on a second monitor means the
    /// dialog appears on a different screen from the work it is about.</para></summary>
    public IntPtr Owner { get; init; }
}
