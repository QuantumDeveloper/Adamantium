namespace Adamantium.UI.Controls;

/// <summary>Which edge a <see cref="TabControl"/>'s tab strip sits on. Each value selects its own control template (via a
/// theme trigger), so a placement can fully restyle the strip - e.g. rotated headers for a docking-style Left/Right.</summary>
public enum TabStripPlacement
{
    Top,
    Bottom,
    Left,
    Right
}
