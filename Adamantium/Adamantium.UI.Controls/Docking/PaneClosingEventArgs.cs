using System;

namespace Adamantium.UI.Controls.Docking;

/// <summary>Raised before a pane leaves the layout, and BEFORE anything is removed. Set <see cref="Cancel"/> to refuse:
/// the pane stays exactly where it was, as if the close had never been asked for.
/// <para>This is what a document with unsaved work answers on. It matters most for the BULK operations - "close all",
/// "close others" - where one refusal must stop that pane and no other: they close one at a time, through here, so a
/// single "no" never turns into "nothing closed" or "everything closed anyway".</para></summary>
public class PaneClosingEventArgs : EventArgs
{
    public PaneClosingEventArgs(string paneId, bool canRestore)
    {
        PaneId = paneId;
        CanRestore = canRestore;
    }

    public string PaneId { get; }

    /// <summary>Whether closing PUTS IT AWAY rather than destroys it - true for a tool, which comes back through
    /// <see cref="DockingArea.RestorePane"/>. A refusal matters far more when the answer here is false.</summary>
    public bool CanRestore { get; }

    /// <summary>Set true to refuse: this pane stays, and a bulk close carries on with the rest.</summary>
    public bool Cancel { get; set; }

    /// <summary>Set true to stop the WHOLE operation, not just this pane - what "Cancel" means in an editor's
    /// save-before-closing dialog. Without it, a bulk close would keep asking about every remaining tab after the user
    /// has already said stop. Implies <see cref="Cancel"/>.</summary>
    public bool CancelAll { get; set; }
}
