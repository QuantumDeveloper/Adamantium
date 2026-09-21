using System;
using System.Collections.Generic;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// Raised by <see cref="DockingArea.PaneTearingOff"/> just before panes leave the layout for a window of their own, and
/// BEFORE anything is moved. Set <see cref="Cancel"/> to refuse: the tab goes back into its strip, or the panel stays
/// docked, exactly as if the gesture had never crossed its threshold.
/// <para>Refusing a tear-off is a different statement from refusing a DOCK, which is why it is a different event: a pane
/// may be perfectly welcome to move around inside the window and still have no business in one of its own (a viewport
/// that needs the main swap chain), or the other way about.</para>
/// </summary>
public class PaneTearingOffEventArgs : EventArgs
{
    public PaneTearingOffEventArgs(IReadOnlyList<string> panes, bool isWholePanel)
    {
        Panes = panes;
        IsWholePanel = isWholePanel;
    }

    /// <summary>Every pane that would leave - one for a dragged tab, all of them for a panel dragged by its caption.</summary>
    public IReadOnlyList<string> Panes { get; }

    /// <summary>Whether the whole PANEL is going (dragged by its caption) rather than a single tab.</summary>
    public bool IsWholePanel { get; }

    /// <summary>Set true to refuse: nothing leaves, and no window is opened.</summary>
    public bool Cancel { get; set; }
}
