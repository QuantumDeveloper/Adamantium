using System;

namespace Adamantium.UI.Core.Input;

/// <summary>
/// The drag "ghost" - a floating, click-through, always-on-top image that follows the cursor during a drag. It is a REAL
/// top-level OS window (so it escapes its origin window's bounds and is never clipped), showing a static CPU bitmap the OS
/// composites with per-pixel alpha - identical behaviour on every platform (docs/DRAG_DROP_PLAN.md Fixed decision 2).
/// One shared bitmap; each platform only differs in the pixel format it hands the compositor.
/// </summary>
public interface IDragGhost : IDisposable
{
    /// <summary>Show the ghost at a screen position, pixels = premultiplied BGRA (top-down, 4 bytes/px, width*height*4).</summary>
    void Show(byte[] premultipliedBgra, int width, int height, int screenX, int screenY);

    /// <summary>Move the ghost to a new screen position (top-left). Cheap - no bitmap re-upload.</summary>
    void Move(int screenX, int screenY);

    /// <summary>Hide the ghost (kept alive for reuse; Dispose destroys the window).</summary>
    void Hide();
}
