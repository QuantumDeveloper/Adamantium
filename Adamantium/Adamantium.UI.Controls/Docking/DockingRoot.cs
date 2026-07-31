using System.Collections.Generic;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// One root of a layout: a screen rectangle plus the tree inside it. The main window and every floating window are
/// EQUAL roots - a floating pane is not a special case, or every operation would fork into "in the main window" and
/// "in a floating one", and moving a pane between them would stop being a move and become interop.
/// <para>This rectangle is the ONLY absolute geometry in a layout. Everything below is fractions, which is what lets a
/// saved layout survive a different window size, a different monitor or a different resolution untouched.</para>
/// </summary>
public class DockingRoot
{
    public DockingRoot(PaneNode content, bool isMain = false)
    {
        Content = content;
        IsMain = isMain;
    }

    /// <summary>True for the root that lives in the application's main window (there is at most one).</summary>
    public bool IsMain { get; set; }

    /// <summary>Where the window sits, in SCREEN coordinates. Restoring checks it against the available screens - a
    /// window saved on a monitor that is no longer there has to come back somewhere visible.</summary>
    public Rect Bounds { get; set; }

    public PaneNode Content { get; set; }

    /// <summary>
    /// The panels PUT AWAY against each edge of this root - a strip of tabs on the edge and nothing more. They are NOT
    /// in <see cref="Content"/>: a panel that is not part of the layout must not be part of the layout's structure.
    /// <para>Keeping them in the tree is what made every kind of drop able to disturb them - a band split them off their
    /// own edge, a divider wrote sizes onto them, a normalise unfolded them, and "which edge is it on" had to be worked
    /// out from a position in a tree it had no business being in. Here they simply belong to an edge, and nothing that
    /// happens inside the tree reaches them (rule 3b).</para>
    /// </summary>
    public Dictionary<DockZone, List<PaneGroupNode>> Bars { get; } = new()
    {
        [DockZone.Left] = [],
        [DockZone.Top] = [],
        [DockZone.Right] = [],
        [DockZone.Bottom] = []
    };

    /// <summary>Which edge a put-away panel is on, or <see cref="DockZone.None"/> if it is not put away here.</summary>
    public DockZone EdgeOfBarred(PaneGroupNode group)
    {
        foreach (var pair in Bars)
        {
            if (pair.Value.Contains(group)) return pair.Key;
        }

        return DockZone.None;
    }
}
