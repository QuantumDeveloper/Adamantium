using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>
/// The seam between a <see cref="ScrollViewer"/> and the element that actually scrolls its content - a physical
/// <see cref="ScrollContentPresenter"/> today, a virtualizing panel later. Replaces WPF's sprawling IScrollInfo: the
/// viewer owns scroll <em>policy</em> (wheel step, page size, bar visibility) and the content owns the
/// <em>mechanism</em> (given a desired offset, translate or realize). Metrics are size/vector valued and a single
/// event announces any change, so the viewer never juggles six separate doubles nor a zoo of
/// LineUp/PageDown/MouseWheel methods.
/// </summary>
public interface IScrollableContent
{
    /// <summary>Total size of the scrolled content.</summary>
    Size Extent { get; }

    /// <summary>Size of the visible window onto the content.</summary>
    Size Viewport { get; }

    /// <summary>Top-left of the viewport within the extent; always within [0, Extent - Viewport].</summary>
    Vector2 Offset { get; }

    /// <summary>The offset the content's LAST layout pass actually realized/arranged its window for (a virtualizing panel
    /// snapshots this at measure). A host translating the content must use THIS - not <see cref="Offset"/>, which can be a
    /// newer value the window hasn't realized yet - so the translation and the realized window agree and the leading edge
    /// never shows a gap. For a non-virtualizing content it equals <see cref="Offset"/>.</summary>
    Vector2 RealizedOffset { get; }

    /// <summary>Whether the viewer permits scrolling on the horizontal axis (the content measures unbounded there).</summary>
    bool CanScrollHorizontally { get; set; }

    /// <summary>Whether the viewer permits scrolling on the vertical axis (the content measures unbounded there).</summary>
    bool CanScrollVertically { get; set; }

    /// <summary>The viewer requests a new scroll position; the content clamps it, applies it, and raises
    /// <see cref="ScrollMetricsChanged"/> if anything actually changed.</summary>
    void SetOffset(Vector2 offset);

    /// <summary>Raised whenever <see cref="Extent"/>, <see cref="Viewport"/>, or <see cref="Offset"/> changes, so the
    /// viewer can refresh its scrollbars.</summary>
    event EventHandler ScrollMetricsChanged;
}

/// <summary>Finding the scrollable content under a host. Two hosts ask it - the <see cref="ScrollContentPresenter"/> and
/// the tab strip's <see cref="TabStripScroller"/> - and both mean the same thing by it, so it is answered once here
/// rather than walked separately in each.</summary>
public static class ScrollableContent
{
    /// <summary>The first <see cref="IScrollableContent"/> in <paramref name="root"/>'s visual subtree, or null when the
    /// content scrolls as one piece (no virtualizing panel inside) and the host must translate it itself.</summary>
    public static IScrollableContent FindIn(IUIComponent root)
    {
        if (root == null) return null;
        foreach (var child in root.VisualChildren)
        {
            if (child is IScrollableContent scrollable) return scrollable;
            var deeper = FindIn(child);
            if (deeper != null) return deeper;
        }
        return null;
    }
}
