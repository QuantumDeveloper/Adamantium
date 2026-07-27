using System;

namespace Adamantium.Win32.Ole;

/// <summary>The <c>MK_*</c> mouse/modifier bits the OS hands to <see cref="IDropTarget"/> and
/// <see cref="IDropSource.QueryContinueDrag"/> - the only reliable key state during a native drag (the source app owns
/// the input queue, so our own message-driven modifier tracking is stale).</summary>
[Flags]
public enum OleKeyState
{
    None = 0,
    LeftButton = 0x0001,
    RightButton = 0x0002,
    Shift = 0x0004,
    Control = 0x0008,
    MiddleButton = 0x0010,
    Alt = 0x0020,
}
