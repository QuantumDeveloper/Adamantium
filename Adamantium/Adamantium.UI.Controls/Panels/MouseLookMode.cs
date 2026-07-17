namespace Adamantium.UI.Controls.Panels;

/// <summary>How a <see cref="RenderTargetPanel"/> feeds a hosted game a relative mouse delta (mouse-look). In a relative
/// mode the panel hides the cursor, holds it centred and synthesizes <c>RawMouseMove</c> events with the raw delta, so a
/// camera can be rotated with no limit even when the pointer reaches the window edge (replacing OS raw input).</summary>
public enum MouseLookMode
{
    /// <summary>No mouse-look: the cursor is a normal pointer and the game gets no relative delta (default).</summary>
    None,

    /// <summary>Look only WHILE a mouse button is held on the panel (editor-style drag-to-look); the cursor is hidden for
    /// the duration of the drag and restored on release.</summary>
    Drag,

    /// <summary>Look CONTINUOUSLY while the panel is focused (shooter-style): click to engage, then any mouse movement
    /// drives the camera with no button held; the cursor stays hidden until the panel loses focus.</summary>
    Continuous
}
