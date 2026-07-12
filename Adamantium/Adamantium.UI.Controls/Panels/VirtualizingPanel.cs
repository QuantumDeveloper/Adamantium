using System;
using System.Collections.Generic;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;

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

    // As an items host, this panel's DesiredSize is the virtual extent (count×cell) computed in MeasureVirtualized -
    // it does NOT depend on any realized tile's measured size. So the layout manager must NOT let a tile's queue-drained
    // re-measure propagate an InvalidateMeasure back up into this panel: that spurious re-dirty is what span the layout
    // pass to MaxPassIterations (the whole realize backlog draining in ONE pass instead of one slice per frame). As a
    // plain container (no owner) the size tracks children, so defer to the base (fixed Width+Height still a boundary).
    public override bool IsMeasureBoundary => IsItemsHost || base.IsMeasureBoundary;

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

    // The offset the last measure realized/arranged the window for (see IScrollableContent.RealizedOffset). A host that
    // translates this panel must use this, not Offset, or the translation and the realized window disagree for a frame.
    public Vector2 RealizedOffset => _passOffset;
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

        // Re-realize the window ONLY when the offset actually shifts which items are on screen (crosses a cell/row
        // boundary). A high-resolution wheel / touchpad emits a stream of SUB-PIXEL scroll deltas, and re-measuring the
        // whole virtualized window on each one - just to land on the SAME first/last - churned the layout every frame:
        // it re-pushed the scroll metrics (re-rendering the whole scrollbar) and re-ran SetWindow, and an occasional
        // full render walk landing on that perpetual churn dropped a just-(re)bound cell for a frame (the "random empty
        // cell"). Within a row the content still slides smoothly (the ScrollContentPresenter translates this panel by
        // -offset) and the thumb still tracks (RaiseMetrics), but the realized window is left untouched.
        var windowMoves = RealizedWindowMovesFor(_offset, clamped);
        _offset = clamped;
        if (windowMoves) InvalidateMeasure();   // the on-screen set changes -> realize/measure the new window
        else RaiseMetrics();                     // same window: only the translation + the scrollbar thumb follow
    }

    /// <summary>Does moving the scroll offset from <paramref name="from"/> to <paramref name="to"/> change which items
    /// fall in the realized window (cross a cell/row boundary)? Base returns true (always re-realize - the safe default);
    /// a uniform-cell panel overrides it so a sub-pixel move that stays within the current row skips the re-window.</summary>
    protected virtual bool RealizedWindowMovesFor(Vector2 from, Vector2 to) => true;

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
            // The NEW extent can be SMALLER than the one _offset was clamped to above (e.g. the cells just shrank): an
            // offset that was valid against the old, larger extent now over-scrolls the content off the top/left. Re-clamp
            // to the new extent and, if it moved, schedule a follow-up pass so the window realizes at the corrected offset.
            var reclamped = ClampOffset(_offset, _extent, _viewport);
            if (reclamped != _offset)
            {
                _offset = reclamped;
                _passOffset = reclamped;
                // The window above was realized for the PRE-clamp offset, but the arrange positions against _passOffset
                // (now the corrected offset) - so the realized window and the translation would disagree for THIS frame:
                // a gap at the leading edge that only fills on the next pass. Re-realize the window for the corrected
                // offset NOW so window + arrange agree this frame. The extent is offset-independent (item count x cell), so
                // re-realizing can't shrink it again -> no loop. (Still schedule a follow-up pass as a safety net.)
                MeasureVirtualized(availableSize, _offset);
                LayoutManager.For(this).InvalidateMeasureNextPass(this);
            }
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
            if (ReferenceEquals(child, _loadingOverlay)) continue;   // panel-owned loading overlay, not a generator container
            if (generator.IndexFromContainer(child) >= 0) continue;   // in the realized window - keep
            child.Visibility = Visibility.Collapsed;
            generator.ReclaimDetached(child);
        }
    }

    // ---- Loading overlay: ONE hit-transparent shimmer over the not-yet-realized region ----
    // Replaces per-slot skeletons. A single overlay visual covers the BOUNDING BOX of the still-deferred slots, so its
    // cost is O(1) per frame no matter how many tiles are pending (per-slot skeletons were O(pending) reconcile + an
    // O(pending) render-dirty from their shimmer, which re-froze a big cold fill). It is:
    //  - HIT-TRANSPARENT (IsHitTestVisible=false) so clicks fall through to the realized tiles beneath it;
    //  - TRANSLUCENT, so the realized tiles at the boundary show through - a partial last row never reads as a gap
    //    (this is why a single box is fine here, unlike an opaque skeleton, which was the objection to one rect);
    //  - swept by a moving highlight band, faded in only after the fill has run a few frames (no flash on a quick fill)
    //    and faded out when the fill completes.
    private Border _loadingOverlay;
    private GradientStop _s1, _s2, _s3;   // the moving band stops (offsets updated each frame)
    private double _shimmerPhase;
    private bool _overlayShown;
    private int _pendingFrames;
    private const int OverlayDelayFrames = 6;     // ~100 ms at 60 fps before it appears - no flash on a fill that clears fast
    private const double ShimmerStep = 0.035;     // band travel per frame (a full sweep ~1.7 s incl. off-screen tails)
    private const double ShimmerHalfBand = 0.16;  // half-width of the bright band, in gradient-offset units
    private static readonly Color ShimmerBase = new(0xFF, 0xFF, 0xFF, 0x14);   // faint tint over the whole region (~8%, RGBA)
    private static readonly Color ShimmerHi = new(0xFF, 0xFF, 0xFF, 0x40);     // the moving highlight band (~25%)

    /// <summary>Reconciles the single loading overlay to the generator's budget-deferred slots: while any are pending it
    /// covers their bounding box with one hit-transparent shimmer; when none are, it fades out. <paramref name="slotRect"/>
    /// maps a slot index to its absolute grid rect - the subclass owns that geometry and calls this from ArrangeVirtualized.
    /// O(pending) is a cheap min/max over the pending indices (no per-slot visual); the drawn cost is O(1) - one overlay.</summary>
    protected void ReconcileLoadingOverlay(Func<int, Rect> slotRect)
    {
        var pending = Owner.ItemContainerGenerator.PendingIndices;
        if (pending.Count == 0)
        {
            _pendingFrames = 0;
            if (_overlayShown) { _overlayShown = false; FadeOverlay(0); }
            return;
        }

        // Bounding box of the pending slots. Contiguous cold fill / a scroll's leading band both give a tight box; a rare
        // fragmented pending over-covers slightly, but the overlay is translucent + hit-transparent so realized tiles under
        // it stay visible and clickable.
        double l = double.MaxValue, t = double.MaxValue, r = double.MinValue, b = double.MinValue;
        for (var i = 0; i < pending.Count; i++)
        {
            var rc = slotRect(pending[i]);
            if (rc.X < l) l = rc.X;
            if (rc.Y < t) t = rc.Y;
            if (rc.Right > r) r = rc.Right;
            if (rc.Bottom > b) b = rc.Bottom;
        }

        _pendingFrames++;
        // Delay: don't flash the overlay on a fill that clears within a few frames (small list / warm cache).
        if (!_overlayShown && _pendingFrames < OverlayDelayFrames) return;

        EnsureOverlay();
        AdvanceShimmer();
        var m = (IMeasurableComponent)_loadingOverlay;
        var box = new Rect(l, t, Math.Max(0, r - l), Math.Max(0, b - t));
        m.Measure(new Size(box.Width, box.Height));
        m.Arrange(box);
        if (_loadingOverlay.Visibility != Visibility.Visible) _loadingOverlay.Visibility = Visibility.Visible;
        if (!_overlayShown) { _overlayShown = true; FadeOverlay(1); }
    }

    private void EnsureOverlay()
    {
        if (_loadingOverlay != null) return;
        _s1 = new GradientStop(ShimmerBase, 0.2);
        _s2 = new GradientStop(ShimmerHi, 0.35);
        _s3 = new GradientStop(ShimmerBase, 0.5);
        var brush = new LinearGradientBrush { StartPoint = new Vector2(0, 0), EndPoint = new Vector2(1, 1) };
        brush.GradientStops.Add(new GradientStop(ShimmerBase, 0));
        brush.GradientStops.Add(_s1);
        brush.GradientStops.Add(_s2);
        brush.GradientStops.Add(_s3);
        brush.GradientStops.Add(new GradientStop(ShimmerBase, 1));
        _loadingOverlay = new Border
        {
            Background = brush,
            CornerRadius = new CornerRadius(4),
            IsHitTestVisible = false,   // clicks fall through to the realized tiles under the overlay
            Opacity = 0
        };
        AddVisualChild(_loadingOverlay);
    }

    // Sweep the bright band across the region. Run the phase past both ends (-half .. 1+half) so the band fully enters and
    // exits BEFORE it wraps - the reset lands while the band is off the visible range, so there is no visible jump. The
    // stop-offset writes don't auto-invalidate the brush, so force one re-record (one visual -> O(1)).
    private void AdvanceShimmer()
    {
        _shimmerPhase += ShimmerStep;
        if (_shimmerPhase > 1 + ShimmerHalfBand) _shimmerPhase = -ShimmerHalfBand;
        _s1.Offset = Math.Clamp(_shimmerPhase - ShimmerHalfBand, 0, 1);
        _s2.Offset = Math.Clamp(_shimmerPhase, 0, 1);
        _s3.Offset = Math.Clamp(_shimmerPhase + ShimmerHalfBand, 0, 1);
        _loadingOverlay.InvalidateRender(false);
    }

    private void FadeOverlay(double to)
    {
        if (_loadingOverlay == null) return;
        _loadingOverlay.BeginAnimation(UIComponent.OpacityProperty, new DoubleAnimation
        {
            From = _loadingOverlay.Opacity,
            To = to,
            Duration = TimeSpan.FromSeconds(0.3),
            FillBehavior = FillBehavior.HoldEnd
        }, () => { _loadingOverlay.Opacity = to; if (to == 0) _loadingOverlay.Visibility = Visibility.Collapsed; });
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
