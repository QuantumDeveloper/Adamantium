using System;

namespace Adamantium.UI.Controls.Docking;

/// <summary>A pane has been closed and is out of the layout. Raised AFTER the fact - closing itself is refused, if at
/// all, through the pane's own policy; this is how anything holding a second account of what is open (a navigation
/// region, a "Windows" menu) hears that it must stop believing its own.</summary>
public class PaneClosedEventArgs : EventArgs
{
    public PaneClosedEventArgs(string paneId, bool canRestore)
    {
        PaneId = paneId;
        CanRestore = canRestore;
    }

    public string PaneId { get; }

    /// <summary>Whether it can be brought back where it was (<see cref="DockingArea.RestorePane"/>) - true for a tool,
    /// which is put away rather than destroyed, and false for a document, which is gone.</summary>
    public bool CanRestore { get; }
}
