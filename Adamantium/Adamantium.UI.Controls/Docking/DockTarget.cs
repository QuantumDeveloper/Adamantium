using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// Where a dragged pane would land: which NODE it is aimed at, on which side of it, and the rectangle it would end up
/// occupying. The preview rectangle is part of the ANSWER rather than something the compass works out for itself -
/// what the user is shown and what the drop then does must come from one calculation, or the preview becomes a
/// decoration that happens to be right most of the time.
/// <para>A node, not a group, because an EDGE anchor - "along the whole left side" - is the same move aimed at the
/// root. There is no second kind of drop and no zone meaning "but of the area this time": the root is a node like any
/// other, and splitting it is what spanning the whole side means.</para>
/// </summary>
public readonly struct DockTarget
{
    public DockTarget(PaneNode node, Rect bounds, DockZone zone, Rect preview, bool isEdge = false)
    {
        Node = node;
        Bounds = bounds;
        Zone = zone;
        Preview = preview;
        IsEdge = isEdge;
    }

    /// <summary>What the drop is aimed at: a group, or the area's root node for an edge anchor.</summary>
    public PaneNode Node { get; }

    /// <summary>The group under the pointer, in the docking area's coordinates. The compass draws its cross from the
    /// centre of it - the same centre the indicators are measured from. It stays the group even when an EDGE is armed:
    /// the cross belongs where the pointer is, only the answer is about the whole area.</summary>
    public Rect Bounds { get; }

    /// <summary><see cref="DockZone.None"/> means the pointer is not over any indicator - dropping does nothing.</summary>
    public DockZone Zone { get; }

    /// <summary>Whether an EDGE anchor is armed rather than one of the cross's five. Only the compass needs it, to pick
    /// which rectangle to preview; the drop already holds the node it is to split.</summary>
    public bool IsEdge { get; }

    /// <summary>Where the pane would be, in the docking area's own coordinates.</summary>
    public Rect Preview { get; }

    public bool IsValid => Node != null && Zone != DockZone.None;
}
