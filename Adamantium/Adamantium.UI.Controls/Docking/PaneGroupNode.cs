using System.Collections.Generic;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// A leaf: one area showing several panes as tabs, one of them active. Panes are referenced BY ID, never by object -
/// the moment a node holds a live pane it stops being data and there is nothing left to serialise.
/// </summary>
public class PaneGroupNode : PaneNode
{
    public List<string> PaneIds { get; } = new();

    /// <summary>Which pane is on top. Clamped to the list, and -1 only while the group is empty (which is a transient
    /// state - an empty group is removed by <see cref="DockingLayout.Normalize"/>).</summary>
    public int ActiveIndex { get; set; } = -1;

    public bool IsEmpty => PaneIds.Count == 0;

    /// <summary>Docked, put away, or being looked at (see <see cref="PaneGroupState"/>). Both unpinned states take only
    /// the room the strip needs - which is measured, not stated (see <see cref="Panels.PaneUnit.Auto"/>).
    /// <para>Folded IN PLACE rather than off to a strip on the root, because that is what an unpinned panel IS here: the
    /// tabs stay where the panel was, so clicking one brings back exactly what was hidden and nothing has to remember
    /// where that was.</para></summary>
    public PaneGroupState State { get; set; } = PaneGroupState.Docked;

    /// <summary>Whether the group owns a length of its own - true only while DOCKED. In both folded states the length is
    /// <see cref="Panels.PaneUnit.Auto"/>: the strip and nothing more, a value that belongs to the fold rather than to
    /// anything the user dragged. A revealed panel is no exception - its body is a flyout over the neighbours, so in the
    /// tree it is still only a strip.</summary>
    public bool OwnsLength => State == PaneGroupState.Docked;

    /// <summary>What it was worth before collapsing, so expanding it again is not a fresh guess at a size.</summary>
    public Panels.PaneLength RestoreLength { get; set; } = Panels.PaneLength.Star;

    public void Add(string paneId)
    {
        PaneIds.Add(paneId);
        if (ActiveIndex < 0) ActiveIndex = 0;
    }

    public void Insert(int index, string paneId)
    {
        if (index < 0) index = 0;
        if (index > PaneIds.Count) index = PaneIds.Count;
        PaneIds.Insert(index, paneId);
        if (ActiveIndex < 0) ActiveIndex = 0;
        else if (index <= ActiveIndex) ActiveIndex++;   // the active pane kept its place, its index moved along
    }

    public bool Remove(string paneId)
    {
        var index = PaneIds.IndexOf(paneId);
        if (index < 0) return false;

        PaneIds.RemoveAt(index);
        // Keep pointing at a real pane: at the one after the removed (its neighbour takes its place), or at the last.
        if (PaneIds.Count == 0) ActiveIndex = -1;
        else if (index < ActiveIndex || ActiveIndex >= PaneIds.Count) ActiveIndex = System.Math.Min(ActiveIndex, PaneIds.Count - 1);
        return true;
    }
}
