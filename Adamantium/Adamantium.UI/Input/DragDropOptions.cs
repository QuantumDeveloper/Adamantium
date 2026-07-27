using Adamantium.UI.Core;

namespace Adamantium.UI.Input;

/// <summary>
/// The drag-drop knobs that are OURS, not the OS's - deliberately kept apart from <see cref="PlatformSettings"/>, which
/// means "what the user configured in the system". Nobody's control panel has an opinion about how deep a scroll edge
/// band is; calling it a platform setting would claim the OS said so.
/// <para>
/// Everything here has a working default and can be changed at startup by an application that wants a different feel.
/// Per-element overrides stay where they belong: scroll speed is also an attached property
/// (<c>DragDrop.AutoScrollSpeed</c>) so one list can differ from the rest.
/// </para>
/// </summary>
public static class DragDropOptions
{
    /// <summary>How long the pointer must dwell over a spring-loadable (a tab, a tree node) before it opens.
    /// Defaults to the user's own hover preference rather than a number of ours.</summary>
    public static double SpringLoadDelayMs { get; set; } = -1;

    /// <summary>How long the pointer must dwell over another of our windows before it is raised to the front.
    /// Same default, same reason.</summary>
    public static double WindowRaiseDelayMs { get; set; } = -1;

    /// <summary>Width of the edge band (px) where a drag starts auto-scrolling a scrollable area.</summary>
    public static double AutoScrollBand { get; set; } = 32;

    /// <summary>Auto-scroll speed (px/sec) for a scroll area that doesn't set <c>DragDrop.AutoScrollSpeed</c> itself.</summary>
    public static double AutoScrollSpeed { get; set; } = 450;

    /// <summary>How far below-right of the cursor the ghost sits, so it never hides the drop point.</summary>
    public static int GhostCursorOffset { get; set; } = 12;

    /// <summary>The dwell values above, resolved: a negative value means "follow the user's hover setting".</summary>
    internal static double ResolveDwell(double configured) =>
        configured >= 0 ? configured : PlatformSettings.HoverTime;
}
