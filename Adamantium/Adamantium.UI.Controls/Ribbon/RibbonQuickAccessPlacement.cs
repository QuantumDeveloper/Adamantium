namespace Adamantium.UI.Controls;

/// <summary>Where the quick-access bar is shown. See docs/RIBBON_PLAN.md §7.1.</summary>
public enum RibbonQuickAccessPlacement
{
    /// <summary>In the caption, through <see cref="TitleBar.LeadingContent"/>.</summary>
    Caption,

    /// <summary>A row of the ribbon's own, under the band.</summary>
    BelowRibbon
}
