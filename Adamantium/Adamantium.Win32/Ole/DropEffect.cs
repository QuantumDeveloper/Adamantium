using System;

namespace Adamantium.Win32.Ole;

/// <summary>OLE <c>DROPEFFECT_*</c>: what a drop target will do with the payload. Exchanged with the OS in every
/// <see cref="IDropTarget"/> call and returned by <c>DoDragDrop</c> as the gesture's outcome.</summary>
[Flags]
public enum DropEffect
{
    None = 0,
    Copy = 1,
    Move = 2,
    Link = 4,
    /// <summary>The target is auto-scrolling (a hint back to the source; we never set it).</summary>
    Scroll = unchecked((int)0x80000000),
}
