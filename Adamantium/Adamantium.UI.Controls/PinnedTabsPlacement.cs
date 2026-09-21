namespace Adamantium.UI.Controls;

/// <summary>Where the PINNED tabs of a strip live.
/// <para><see cref="SeparateRow"/>: a row of their own above the others, wrapping onto further lines when there are many.
/// They then never compete for room with the tabs that come and go, and never scroll out of sight - which is the point
/// of pinning something. This is what Rider does.</para>
/// <para><see cref="SameRow"/>: one row, pinned tabs first. What Visual Studio does; it costs the ordinary tabs the
/// room the pinned ones take.</para></summary>
public enum PinnedTabsPlacement
{
    SeparateRow,
    SameRow
}
