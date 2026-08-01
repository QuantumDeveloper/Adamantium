namespace Adamantium.UI.Controls;

/// <summary>Which side of the tab strip the selection bar runs along. Stated RELATIVE to the strip rather than as
/// top/bottom/left/right, because the strip itself sits on any edge (<see cref="TabStripPlacement"/>) and "top" means
/// nothing on a vertical one - two values cover all four placements without a single meaningless combination.
/// <para><see cref="Inner"/>: against the CONTENT, so the bar reads as the near edge of the open page - a strip on top
/// puts it under the tab. <see cref="Outer"/>: against the strip's outer edge, so the bar reads as a marker laid over
/// the tab - a strip on top puts it above the tab, the way VS Code marks the open file.</para></summary>
public enum TabIndicatorPlacement
{
    Inner,
    Outer
}
