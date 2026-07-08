using System;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// Base for panels that can host an <see cref="ItemsControl"/>'s items with virtualization: it owns the whole mechanism
/// (the <see cref="IScrollableContent"/> seam, the realized window, realize/recycle through the generator, and the
/// measure/arrange dispatch) and leaves only the geometry to subclasses (StackPanel = 1D, WrapPanel = 2D). There is no
/// user "turn virtualization off" knob: as a plain container (no owner) it lays its <see cref="Panel.Children"/> out via
/// <see cref="MeasurePlain"/>/<see cref="ArrangePlain"/> exactly as before; as an items host it realizes only the visible
/// window. When given an unbounded extent on the scroll axis (no viewport) it realizes everything (the degenerate case)
/// and reports it via <see cref="OnNoViewport"/> instead of silently being slow.
/// </summary>
public abstract class VirtualizingPanel : Panel, IScrollableContent
{
    private Size _extent;
    private Size _viewport;
    private Vector2 _offset;
    // The offset the current measure realized its window against. Arrange positions items with THIS, not a fresh read of
    // _offset: a fast scroll can change _offset between the window's measure phase and its arrange phase, and two
    // different offsets would position an item where the measure didn't realize it. One snapshot per pass keeps the
    // realized window and the arranged positions consistent.
    private Vector2 _passOffset;

    // The virtualizing panel's own desired size is count*itemExtent - INDEPENDENT of its children. So while it realizes
    // /rebinds its window inside its own measure/arrange, a container's InvalidateMeasure (the rebind re-resolves the
    // item template's AffectsMeasure bindings) must NOT propagate up and re-invalidate the panel: that would make the
    // layout manager run a SECOND full MeasureVirtualized (re-realizing the whole window) on every pass - a ~2x layout
    // cost on every scroll/relayout frame. Muting child-originated invalidation during the pass reflects that the
    // panel's measure does not depend on its children (the plan's "propagate up only where the parent depends on the
    // child" principle); the panel re-measures each realized container itself inside MeasureVirtualized.
    private bool _inLayout;

    public override void InvalidateMeasure()
    {
        if (_inLayout) return;
        base.InvalidateMeasure();
    }

    public override void InvalidateArrange()
    {
        if (_inLayout) return;
        base.InvalidateArrange();
    }

    /// <summary>The items control this panel hosts (set by the <see cref="ItemsPresenter"/>); null = plain container.</summary>
    internal ItemsControl Owner { get; private set; }

    protected bool IsItemsHost => Owner != null;

    public Size Extent => _extent;
    public Size Viewport => _viewport;
    public Vector2 Offset => _offset;
    public bool CanScrollHorizontally { get; set; } = true;
    public bool CanScrollVertically { get; set; } = true;
    public event EventHandler ScrollMetricsChanged;

    /// <summary>Switches the panel into items-host mode for <paramref name="owner"/>; it now virtualizes its items.</summary>
    internal void AttachOwner(ItemsControl owner)
    {
        Children.Clear();   // drop any plain children; the window is managed via the generator from here
        Owner = owner;
        // Do NOT clip on the panel itself. In transform-only scroll the ScrollContentPresenter SLIDES this panel by -offset,
        // so a self-clip would move WITH the panel (its clip rect lands at [-offset, -offset+viewport] in world space) and
        // scissor out the very tiles now scrolled into view - the "only the first page renders" bug. Buffer/overflow tiles
        // are trimmed by the ScrollContentPresenter's clip instead, which stays anchored at the viewport (world origin) and
        // is the correct place to bound the list. (A virtualizing panel is always hosted inside that clipping presenter.)
        InvalidateMeasure();
    }

    /// <summary>Drops the realized window AND the pooled containers (e.g. the collection reset) so the next measure
    /// rebuilds from scratch. Detaches every container the panel holds (realized + pooled), not just the visible ones.</summary>
    internal void Revirtualize()
    {
        foreach (var child in VisualChildren.ToList())
        {
            RemoveVisualChild(child);
            RemoveLogicalChild(child);
        }
        Owner?.ItemContainerGenerator.Clear();
        InvalidateMeasure();
    }

    public void SetOffset(Vector2 offset)
    {
        var clamped = ClampOffset(offset, _extent, _viewport);
        if (clamped == _offset) return;
        _offset = clamped;
        InvalidateMeasure();   // a new window must be realized/measured
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!IsItemsHost) return MeasurePlain(availableSize);

        _inLayout = true;
        try
        {
            _offset = ClampOffset(_offset, _extent, _viewport);
            _passOffset = _offset;   // snapshot: the matching arrange positions against exactly this
            var extent = MeasureVirtualized(availableSize, _offset);
            _extent = extent;
            // Occupy the viewport on a bounded axis, the extent on an axis the parent left unbounded.
            _viewport = new Size(
                double.IsInfinity(availableSize.Width) ? extent.Width : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? extent.Height : availableSize.Height);
        }
        finally { _inLayout = false; }
        RaiseMetrics();
        return _viewport;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (!IsItemsHost) return ArrangePlain(finalSize);

        _inLayout = true;
        try
        {
            _viewport = finalSize;
            // Position against the SAME offset the measure realized/decided visibility with - NOT a fresh _offset (which
            // a mid-pass scroll may have moved). _offset itself is left as-is so the next pass picks up that newer value.
            var arrangeOffset = ClampOffset(_passOffset, _extent, finalSize);
            ArrangeVirtualized(finalSize, arrangeOffset);
            HideUnmappedContainers();
        }
        finally { _inLayout = false; }
        RaiseMetrics();
        return finalSize;
    }

    // Plain (non items-host) layout — the panel used as an ordinary container. Subclass = its existing measure/arrange.
    protected abstract Size MeasurePlain(Size availableSize);
    protected abstract Size ArrangePlain(Size finalSize);

    // Virtualized layout — realize/measure/arrange only the visible window (subclass owns the geometry).
    protected abstract Size MeasureVirtualized(Size availableSize, Vector2 offset);
    protected abstract void ArrangeVirtualized(Size finalSize, Vector2 offset);

    /// <summary>Attaches (if new) and shows the container for <paramref name="index"/>, which the generator's SetWindow
    /// has already realized/rebound. Falls back to a direct realize for the pre-SetWindow probe. The container keeps its
    /// visual + GPU buffers across reuse (it is rebound, never detached/recreated).</summary>
    protected IUIComponent RealizeInWindow(int index)
    {
        var container = Owner.ItemContainerGenerator.ContainerFromIndex(index)
                        ?? Owner.ItemContainerGenerator.Realize(index);
        if (container.VisualParent != this)   // a reused container is already a child; only a brand-new one needs attaching
        {
            AddVisualChild(container);
            AddLogicalChild(container);
        }
        container.Visibility = Visibility.Visible;
        return container;
    }

    /// <summary>
    /// Enforces the invariant "a container is visible IFF it is in the realized window". A fast scroll can leave a
    /// container attached and still visible but no longer mapped to any index by the generator; ArrangeVirtualized only
    /// positions the realized indices, so such a container freezes at its last spot, and over a fast scroll these ghosts
    /// pile up overlapping the real items (and, recorded, blur into impossible-looking labels). Hide every visible
    /// container the generator no longer knows, and hand it back to the pool so it is reused rather than leaked.
    /// </summary>
    private void HideUnmappedContainers()
    {
        var generator = Owner.ItemContainerGenerator;
        foreach (var child in VisualChildren)
        {
            if (child.Visibility != Visibility.Visible) continue;
            if (generator.IndexFromContainer(child) >= 0) continue;   // in the realized window - keep
            child.Visibility = Visibility.Collapsed;
            generator.ReclaimDetached(child);
        }
    }

    /// <summary>Called when the scroll axis is unbounded (no viewport) so everything has to be realized. Override to log.</summary>
    protected virtual void OnNoViewport()
    {
        System.Diagnostics.Debug.WriteLine(
            $"[Adamantium] {GetType().Name} has no bounded viewport on its scroll axis - realizing all {Owner?.Items.Count} items (not virtualizing). Wrap the ItemsControl in a sized ScrollViewer.");
    }

    private void RaiseMetrics() => ScrollMetricsChanged?.Invoke(this, EventArgs.Empty);

    private static Vector2 ClampOffset(Vector2 offset, Size extent, Size viewport)
    {
        var maxX = Math.Max(0, extent.Width - viewport.Width);
        var maxY = Math.Max(0, extent.Height - viewport.Height);
        return new Vector2(Math.Clamp(offset.X, 0, maxX), Math.Clamp(offset.Y, 0, maxY));
    }
}
