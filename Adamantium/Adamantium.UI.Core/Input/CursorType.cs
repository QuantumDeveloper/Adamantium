namespace Adamantium.UI.Core.Input;

/// <summary>
/// The standard pointer shapes, named by MEANING rather than by any one OS's identifiers - every desktop platform ships
/// this set (Win32 <c>IDC_*</c>, macOS <c>NSCursor</c>, X11/Wayland cursor themes), so a control asks for
/// "the resize-vertical cursor" and each platform hands over its own.
/// </summary>
public enum CursorType
{
    /// <summary>No pointer at all - the cursor is hidden while over the element.</summary>
    None,
    Arrow,
    AppStarting,
    Crosshair,
    Hand,
    Help,
    IBeam,
    /// <summary>The "not allowed" slashed circle - a drop that cannot happen here.</summary>
    No,
    SizeAll,
    SizeNESW,
    SizeNS,
    SizeNWSE,
    SizeEWE,
    UpArrow,
    Wait,
    /// <summary>Drag feedback: this drop will COPY (arrow + plus).</summary>
    DragCopy,
    /// <summary>Drag feedback: this drop will LINK (arrow + shortcut arrow).</summary>
    DragLink,
    /// <summary>A cursor loaded from a file - see <see cref="Cursor.FilePath"/>.</summary>
    Custom,
}
