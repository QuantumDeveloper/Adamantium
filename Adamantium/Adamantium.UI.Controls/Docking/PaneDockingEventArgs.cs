using System;
using System.Collections.Generic;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// Raised by <see cref="DockingArea.PaneDocking"/> just before panes are docked somewhere - the moment the user let go
/// over an indicator, and BEFORE the layout is touched. Set <see cref="Cancel"/> to refuse the move; what was being
/// dragged stays exactly where it was.
/// <para>This is the escape hatch for the rules a set of zones cannot express - "not next to that panel", "not while
/// this document is running" - and it is the only one: <see cref="Pane.Allowed"/> answers where a pane may go AT ALL,
/// and answers it in data that serialises and can be read at a glance. A predicate would express anything and none of
/// that, which is why the vocabulary is split in two.</para>
/// <para>Raised once, ON THE DROP, not while the pointer moves: the compass shows what the ZONES allow, and asking the
/// application hundreds of times a second - with whatever an application does in a handler - is not a question that can
/// be asked per mouse move.</para>
/// </summary>
public class PaneDockingEventArgs : EventArgs
{
    public PaneDockingEventArgs(IReadOnlyList<string> panes, PaneNode target, DockZone zone)
    {
        Panes = panes;
        Target = target;
        Zone = zone;
    }

    /// <summary>Every pane that would move - one for a dragged tab, all of them for a panel or a floating window.</summary>
    public IReadOnlyList<string> Panes { get; }

    /// <summary>What it would be docked against: a group to be tabbed into, or the root for an edge anchor.</summary>
    public PaneNode Target { get; }

    /// <summary>Which side of the target, or <see cref="DockZone.Center"/> for "as another tab".</summary>
    public DockZone Zone { get; }

    /// <summary>Set true to refuse: nothing moves and the floating window stays where it is.</summary>
    public bool Cancel { get; set; }
}
