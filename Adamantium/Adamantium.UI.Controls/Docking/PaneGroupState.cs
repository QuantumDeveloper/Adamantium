namespace Adamantium.UI.Controls.Docking;

/// <summary>How a pane group is showing itself. Three states rather than a pinned/unpinned flag, because unpinned is two
/// different things - put away, and looked at without being put back - and every editor treats them as such.</summary>
public enum PaneGroupState
{
    /// <summary>Pinned into the layout: its header, its body, and its tabs along the bottom. It owns a length and takes
    /// that room from its neighbours.</summary>
    Docked,

    /// <summary>Unpinned and put away. Nothing is left in the layout but the tab strip against the edge, labels turned
    /// on their side; the room it held went back to its neighbours.</summary>
    Collapsed,

    /// <summary>Unpinned but being looked at: the strip STAYS against the edge exactly as it was and the body appears
    /// OVER the neighbouring content instead of pushing it aside - so a glance at a tool costs the layout nothing and
    /// gives nothing back when it closes. Pinning it again is what returns it to <see cref="Docked"/>, and only then do
    /// its tabs go back along the bottom.</summary>
    Revealed
}
