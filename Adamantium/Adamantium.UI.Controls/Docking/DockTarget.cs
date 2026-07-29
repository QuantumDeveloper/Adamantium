using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// Where a dragged pane would land: which group it is aimed at, on which side of it, and the rectangle it would end up
/// occupying. The preview rectangle is part of the ANSWER rather than something the compass works out for itself -
/// what the user is shown and what the drop then does must come from one calculation, or the preview becomes a
/// decoration that happens to be right most of the time.
/// </summary>
public readonly struct DockTarget
{
    public DockTarget(PaneGroupNode group, Rect bounds, DockZone zone, Rect preview)
    {
        Group = group;
        Bounds = bounds;
        Zone = zone;
        Preview = preview;
    }

    public PaneGroupNode Group { get; }

    /// <summary>The aimed-at group's rectangle, in the docking area's coordinates. The compass is placed from its
    /// centre - the same centre the indicators are measured from.</summary>
    public Rect Bounds { get; }

    /// <summary><see cref="DockZone.None"/> means the pointer is not over any indicator - dropping does nothing.</summary>
    public DockZone Zone { get; }

    /// <summary>Where the pane would be, in the docking area's own coordinates.</summary>
    public Rect Preview { get; }

    public bool IsValid => Group != null && Zone != DockZone.None;
}
