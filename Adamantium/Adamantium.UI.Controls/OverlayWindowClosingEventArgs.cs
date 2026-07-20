using System;

namespace Adamantium.UI.Controls;

/// <summary>Raised by <see cref="OverlayWindow.Closing"/>: set <see cref="Cancel"/> to keep the window open.</summary>
public class OverlayWindowClosingEventArgs : EventArgs
{
    public OverlayWindowClosingEventArgs(object result) => Result = result;

    /// <summary>The result the window is closing with (what <see cref="OverlayWindow.Result"/> becomes if not cancelled).</summary>
    public object Result { get; }

    /// <summary>Set to true to cancel the close and keep the window open.</summary>
    public bool Cancel { get; set; }
}
