using System;

namespace Adamantium.UI.Controls.Docking;

/// <summary>A saved layout names a pane the area does not have. The application is asked to make it - from
/// <see cref="RestoreKey"/>, which is whatever it wrote down for exactly this purpose - and answers by setting
/// <see cref="Pane"/>. Leaving it null means "that one is gone", and the layout is applied without it.</summary>
public class PaneRestoringEventArgs : EventArgs
{
    public PaneRestoringEventArgs(string paneId, string restoreKey)
    {
        PaneId = paneId;
        RestoreKey = restoreKey;
    }

    /// <summary>The id the layout refers to. The pane handed back must carry it, or the layout will not find it.</summary>
    public string PaneId { get; }

    /// <summary>What the pane wrote down about itself when the layout was saved (<see cref="Docking.Pane.RestoreKey"/>),
    /// or null when it never said anything - a pane the markup declares needs no making.</summary>
    public string RestoreKey { get; }

    /// <summary>The pane the application made, or null if it cannot.</summary>
    public Pane Pane { get; set; }
}
