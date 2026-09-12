namespace Adamantium.UI.Core;

/// <summary>Asking the user for a file, through whatever the operating system puts in front of them. A control that
/// produces a file - a table exporting itself, a document saving - writes to a stream the application hands it; WHERE
/// that stream goes is the user's to say, and this is where they say it.
///
/// <para>Behind a platform, because there is no shared dialog to share: Windows has the shell's, macOS has NSSavePanel,
/// and on Linux it belongs to the desktop environment. What they agree on is the QUESTION - a title, a suggested name,
/// the kinds of file on offer - and that is all <see cref="SaveFileRequest"/> carries.</para></summary>
public static class FileDialog
{
    /// <summary>The platform that answers, registered once at startup. Null where none is written yet - ask
    /// <see cref="IsAvailable"/> rather than reading a cancelled answer as a refusal.</summary>
    public static IFileDialogPlatform Platform { get; set; }

    /// <summary>Whether this platform can ask at all. Worth checking BEFORE offering the action: without it, an
    /// application cannot tell a user who pressed Cancel from a platform that never opened anything, and would sit
    /// there having silently done nothing.</summary>
    public static bool IsAvailable => Platform != null;

    /// <summary>Asks where to save, and returns the full path the user chose - or null if they chose nothing. Null is
    /// the only refusal: nothing is written, and there is no error to report, because cancelling is not one.</summary>
    public static string Save(SaveFileRequest request) => Platform?.Save(request);
}
