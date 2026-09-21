namespace Adamantium.UI.Controls.Docking;

/// <summary>
/// One authored group: WHERE it goes and, optionally, how big it starts. This is the whole vocabulary of markup - the
/// author names a zone, not a share of a split they cannot see. See <see cref="DockingLayout.FromZones"/>.
/// </summary>
public readonly struct ZoneDeclaration
{
    public ZoneDeclaration(DockZone zone, PaneGroupNode group, double size = double.NaN)
    {
        Zone = zone;
        Group = group;
        Size = size;
    }

    public DockZone Zone { get; }

    public PaneGroupNode Group { get; }

    /// <summary>Starting size in pixels along the zone's axis, or NaN for "decide for me".</summary>
    public double Size { get; }
}
