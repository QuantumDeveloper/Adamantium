using Adamantium.MacOS;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;

namespace Adamantium.UI.Platforms.MacOS;

/// <summary>
/// AppKit <see cref="INativeCursors"/>: maps a <see cref="CursorType"/> onto an <c>NSCursor</c> class shape and pushes
/// it as the current cursor.
/// <para>
/// AppKit's catalog is smaller than Win32's, so several shapes fall back to the nearest sibling - this is where those
/// substitutions are decided, and they are deliberate: Wait/AppStarting have no NSCursor (macOS shows the spinning
/// wheel itself, per-application, not per-element), Help/UpArrow have none, and both diagonal resize shapes collapse
/// onto the horizontal one. Everything drag-related - "not allowed", copy, link - IS native here, which is exactly the
/// feedback the drag engine needs.
/// </para>
/// </summary>
internal sealed class MacOSCursors : INativeCursors
{
    public void Apply(Cursor cursor)
    {
        if (cursor == null) return;
        if (cursor.Type == CursorType.None)
        {
            MacOSInterop.Cursor.Hide();
            return;
        }
        MacOSInterop.Cursor.SetCursorType((uint)Native(cursor.Type));
    }

    private static MacOSCursorType Native(CursorType type) => type switch
    {
        CursorType.Crosshair => MacOSCursorType.CrosshairCursor,
        CursorType.Hand => MacOSCursorType.PointingHandCursor,
        CursorType.IBeam => MacOSCursorType.IBeamCursor,
        CursorType.No => MacOSCursorType.OperationNotAllowedCursor,
        CursorType.SizeAll => MacOSCursorType.ClosedHandCursor,          // AppKit's "grabbing and moving" shape
        CursorType.SizeNS => MacOSCursorType.ResizeUpDownCursor,
        CursorType.SizeEWE => MacOSCursorType.ResizeLeftRightCursor,
        CursorType.SizeNESW => MacOSCursorType.ResizeLeftRightCursor,    // no diagonal shape in AppKit
        CursorType.SizeNWSE => MacOSCursorType.ResizeLeftRightCursor,
        CursorType.UpArrow => MacOSCursorType.ResizeUpCursor,
        CursorType.DragCopy => MacOSCursorType.DragCopyCursor,
        CursorType.DragLink => MacOSCursorType.DragLinkCursor,
        // Arrow, AppStarting, Help, Wait and a custom file (NSCursor needs an NSImage, not a .cur) all land here.
        _ => MacOSCursorType.ArrowCursor,
    };
}
