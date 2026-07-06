namespace Adamantium.UI.Core;

/// <summary>How a window may be resized by the user. Mirrors WPF's ResizeMode. With custom chrome the native resize
/// borders (WM_NCHITTEST) honour this; a fully borderless (WS_POPUP) window uses a ResizeGripper for CanResizeWithGrip.</summary>
public enum WindowResizeMode
{
    /// <summary>Fixed size: no resize borders, no maximize.</summary>
    NoResize,

    /// <summary>Can minimize but not resize or maximize.</summary>
    CanMinimize,

    /// <summary>Full resize via the window borders (default).</summary>
    CanResize,

    /// <summary>Resize via a corner grip only (for the fully borderless WS_POPUP mode).</summary>
    CanResizeWithGrip
}
