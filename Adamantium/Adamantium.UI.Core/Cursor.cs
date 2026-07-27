using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Core;

/// <summary>
/// A pointer shape, described rather than realized: a <see cref="CursorType"/> (or a file, for a custom one). It holds
/// NO native handle - the platform's <see cref="INativeCursors"/> resolves and caches that, so the same
/// <c>Cursors.SizeNS</c> means the Win32 <c>IDC_SIZENS</c>, the macOS <c>resizeUpDownCursor</c> or an X11 theme shape
/// without a control ever knowing which.
/// </summary>
public sealed class Cursor
{
    /// <summary>The platform that shows cursors, registered once at startup (Win32 / AppKit / X11). Null before it is -
    /// setting a cursor is then simply a no-op, never a crash.</summary>
    public static INativeCursors Platform { get; set; }

    public Cursor(CursorType type)
    {
        Type = type;
    }

    /// <summary>A custom cursor from a file (a Win32 <c>.cur</c>/<c>.ani</c>, or the platform's own image format).</summary>
    public Cursor(string cursorFile)
    {
        Type = CursorType.Custom;
        FilePath = cursorFile;
    }

    public CursorType Type { get; }

    /// <summary>Where the custom cursor is loaded from; null for a standard <see cref="CursorType"/>.</summary>
    public string FilePath { get; }

    public static Cursor Default => Cursors.Arrow;

    /// <summary>Naming a standard shape IS naming a cursor: <c>e.DragCursor = CursorType.Hand</c> reads as intent and
    /// costs nothing, because it hands back the shared catalogue entry rather than building one. It also lets code that
    /// must not deal in UI objects - a view-model choosing drop feedback - stay on the plain enum.</summary>
    public static implicit operator Cursor(CursorType type) => Cursors.Of(type);
}
