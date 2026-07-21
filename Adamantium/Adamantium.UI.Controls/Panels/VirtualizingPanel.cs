using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.RoutedEvents;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls.Panels;

/// <summary>
/// Base for panels that can host an <see cref="ItemsControl"/>'s items with virtualization: it owns the whole mechanism
/// (the <see cref="IScrollableContent"/> seam, the realized window, realize/recycle through the generator, and the
/// measure/arrange dispatch) and leaves only the geometry to subclasses (StackPanel = 1D, WrapPanel = 2D). As a plain
/// container (no owner) it lays its <see cref="Panel.Children"/> out via <see cref="MeasurePlain"/>/<see cref="ArrangePlain"/>
/// exactly as before; as an items host it realizes only the visible window. When given an unbounded extent on the scroll
/// axis (no viewport) it realizes everything (the degenerate case) and reports it via <see cref="OnNoViewport"/> instead of
/// silently being slow. Set <see cref="IsVirtualizing"/> = false for a small mixed-height host (a menu: 34px rows + a 9px
/// separator) where the uniform-cell assumption would give every item the same slot - then it realizes all items and stacks
/// them by their OWN measured extents.
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
    public override bool IsMeasureBoundary => (IsItemsHost && IsVirtualizing) || base.IsMeasureBoundary;

    public override void InvalidateMeasure()
    {
        if (_inLayout) return;
        base.InvalidateMeasure();
    }

    /// <summary>Whether to virtualize when hosting items (default true). Set false for a small, mixed-height host (a menu)
    /// so every item is realized and measured/arranged at its OWN size instead of a uniform cell extent.</summary>
    public static readonly AdamantiumProperty IsVirtualizingProperty = AdamantiumProperty.Register(nameof(IsVirtualizing),
        typeof(bool), typeof(VirtualizingPanel), new PropertyMetadata(true));

    public bool IsVirtualizing
    {
        get => GetValue<bool>(IsVirtualizingProperty);
        set => SetValue(IsVirtualizingProperty, value);
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
        // The loop above also detached the skeleton cards - drop their now-dangling pool/active/set so the next fill
        // rebuilds fresh ones (else RentSkeleton hands back a card that is no longer a visual child and never renders,
        // which made skeletons vanish after an ItemTemplate switch / any regenerate).
        ResetSkeletons();
        InvalidateMeasure();
    }

    /// <summary>Applies a collection change to the realized window IN PLACE - no teardown. The generator reindexes its
    /// realized set, the added/removed slots (re)bind on the next measure via <c>SetWindow</c>, and every UNCHANGED
    /// container keeps its measured/arranged/rendered state. A full <see cref="Revirtualize"/> on every add instead
    /// re-created every container and re-probed the item extent off a not-yet-settled fresh container, which shifted the
    /// list on each add and left the window in a state that then mis-hit-tested on scroll (dynamic-only, never static -
    /// because a static list never runs this path). Reset/Move/Replace still rebuild (rare; correctness over cleverness).</summary>
    internal void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        if (Owner?.ItemContainerGenerator is not { } generator) return;

        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                // Shift realized indices at/after the insert up; the inserted slot(s) are now an unmapped gap that the
                // next SetWindow fills, and the shifted containers keep their (unchanged) items at their new indices.
                generator.OnItemsInserted(e.NewStartingIndex, e.NewItems.Count);
                InvalidateMeasure();
                break;

            case NotifyCollectionChangedAction.Remove:
                // Recycle the removed slots' containers FIRST (unmap + pool), THEN reindex survivors down - reindexing
                // before recycling would collide a removed key with the survivor shifting onto it. The pooled containers
                // are re-drawn as donors by the next SetWindow, or parked by the arrange's HideUnmappedContainers.
                for (var i = e.OldItems.Count - 1; i >= 0; i--)
                    generator.Recycle(e.OldStartingIndex + i);
                generator.OnItemsRemoved(e.OldStartingIndex, e.OldItems.Count);
                InvalidateMeasure();
                break;

            default:   // Replace / Move / Reset: full rebuild.
                Revirtualize();
                break;
        }
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

    /// <summary>Parks an off-screen container: hide it AND deactivate every binding in its subtree so it drops out of any
    /// shared source's fan-out (no storm on a shared-property change while it sits off screen). It stays attached and
    /// pooled - NO detach, so no structural re-record - and is re-subscribed for free when reused, because the reuse sets
    /// its DataContext which runs RefreshBindings. Deactivate is O(1)-per-binding (SharedSourceRegistry).</summary>
    protected static void ParkContainer(IUIComponent container)
    {
        container.Visibility = Visibility.Collapsed;
        DeactivateSubtreeBindings(container);
    }

    private static void DeactivateSubtreeBindings(IUIComponent node)
    {
        Adamantium.UI.Core.Data.BindingEngine.DeactivateBindings(node);
        foreach (var child in node.VisualChildren) DeactivateSubtreeBindings(child);
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
            if (_skeletonSet.Contains(child)) continue;   // panel-owned loading card, not a generator container
            if (generator.IndexFromContainer(child) >= 0) continue;   // in the realized window - keep
            ParkContainer(child);
            generator.ReclaimDetached(child);
        }
    }

    // ---- Per-slot loading skeletons: one themed ItemSkeletonTemplate card per budget-deferred slot ----
    // A virtualizing fill can defer part of the window past the per-frame bind budget (generator.PendingIndices). Rather
    // than a hole, each deferred slot shows a pulsing placeholder card - the classic skeleton look, per-item and clear.
    // Cards are POOLED (reused across slots/frames, never recreated) and every card is an instanced SDF rect, so a whole
    // screenful is cheap on the GPU. The breathe is theme-authored and SHARED: every card paints with the ONE keyed
    // skeleton brush, whose Opacity a single PulseAnimation drives while this list reports IsLoadingItems (see
    // SyncLoadingState) - a screenful of cards costs one animation, not one per card. Skipped when the ItemsControl has
    // no ItemSkeletonTemplate.
    private readonly Stack<UIComponent> _skeletonPool = new();               // idle cards, ready to reuse
    private readonly Dictionary<int, UIComponent> _activeSkeletons = new();  // slot index -> the card showing there now

    /// <summary>How many skeleton cards are on screen right now (one is reconciled per PENDING slot, every
    /// frame - a cost that scales with the un-filled window, not with what the frame admits).</summary>
    protected int ActiveSkeletonCount => _activeSkeletons.Count;
    private readonly HashSet<IUIComponent> _skeletonSet = new();             // every card built (skip in HideUnmappedContainers)
    private readonly List<int> _recycleBuf = new();                          // scratch: active slots to recycle this frame
    private readonly HashSet<int> _pendingSet = new();                       // scratch: this frame's pending, for O(1) lookup
    private readonly List<(UIComponent card, Rect rect)> _skelArrangeBuf = new();   // scratch: (card, rect) for the parallel arrange pass
    private int _pendingFrames;
    private const int SkeletonDelayFrames = 6;   // ~100 ms at 60 fps before cards appear - no flash on a fill that clears fast
    private const int SkeletonParallelThreshold = 64;   // fan the card arrange across cores only above this many (matches the real-tile arrange)

    /// <summary>Reconciles the pooled per-slot loading cards to the generator's budget-deferred slots: each pending slot
    /// shows a themed <c>ItemSkeletonTemplate</c> card positioned at its grid rect; a slot that binds (or scrolls out)
    /// hands its card back to the pool. <paramref name="slotRect"/> maps a slot index to its absolute grid rect - the
    /// subclass owns that geometry and calls this from ArrangeVirtualized. O(pending): a screenful of pooled instanced cards.</summary>
    protected void ReconcileSkeletons(Func<int, Rect> slotRect)
    {
        var pending = Owner.ItemContainerGenerator.PendingIndices;

        if (pending.Count == 0)
        {
            _pendingFrames = 0;
            if (_activeSkeletons.Count > 0) RecycleAllSkeletons();
            return;
        }

        _pendingFrames++;
        // Don't flash cards on a fill that clears within a few frames (small list / warm cache).
        if (_activeSkeletons.Count == 0 && _pendingFrames < SkeletonDelayFrames) return;

        var template = Owner?.ItemSkeletonTemplate;
        if (template == null) return;   // unthemed ItemsControl - no skeletons

        // Recycle cards whose slot is no longer pending (it bound, or scrolled out of the window).
        _pendingSet.Clear();
        for (var i = 0; i < pending.Count; i++) _pendingSet.Add(pending[i]);
        _recycleBuf.Clear();
        foreach (var slot in _activeSkeletons.Keys)
            if (!_pendingSet.Contains(slot)) _recycleBuf.Add(slot);
        for (var i = 0; i < _recycleBuf.Count; i++) RecycleSkeleton(_recycleBuf[i]);

        // Inset each cell rect by the REAL tile's margin (read from a realized item, never hardcoded) so a card sits
        // exactly where its item's visual would - same footprint, same inter-tile gaps.
        var inset = ItemMargin();

        // Pass 1 (this thread): ensure a card at each pending slot and SHOW it. Renting mutates the pool/active/set + the
        // visual tree, and SyncLoadingState below fires the list's pulse trigger (which mutates the shared
        // AnimationManager) - none of that is thread-safe, so it stays here. Collect (card, rect) for a parallel arrange.
        _skelArrangeBuf.Clear();
        for (var i = 0; i < pending.Count; i++)
        {
            var slot = pending[i];
            if (!_activeSkeletons.TryGetValue(slot, out var card))
            {
                card = RentSkeleton(template);
                if (card == null) return;   // template has no root
                _activeSkeletons[slot] = card;
            }
            if (card.Visibility != Visibility.Visible) card.Visibility = Visibility.Visible;
            var rc = slotRect(slot);
            _skelArrangeBuf.Add((card, new Rect(
                rc.X + inset.Left, rc.Y + inset.Top,
                Math.Max(0, rc.Width - inset.Left - inset.Right),
                Math.Max(0, rc.Height - inset.Top - inset.Bottom))));
        }

        SyncLoadingState();

        // Pass 2: measure + arrange the cards. Each card is an independent leaf (its own geometry; the only shared write
        // is the locked MarkGeometry), so fan them across cores when there are enough to amortise the thread overhead -
        // same as the real-tile arrange above. Small windows stay sequential.
        if (_skelArrangeBuf.Count >= SkeletonParallelThreshold)
            System.Threading.Tasks.Parallel.ForEach(
                System.Collections.Concurrent.Partitioner.Create(0, _skelArrangeBuf.Count),
                range => { for (var i = range.Item1; i < range.Item2; i++) ArrangeSkeleton(_skelArrangeBuf[i]); });
        else
            for (var i = 0; i < _skelArrangeBuf.Count; i++) ArrangeSkeleton(_skelArrangeBuf[i]);
    }

    // The LIST-level loading state (ItemsControl.IsLoadingItems): true exactly while cards are on screen. The theme keys
    // the skeleton shimmer off it - ONE trigger per list starts/stops the pulse on the shared skeleton brush - so a
    // screenful of cards costs one animation, not one per card (which is also one property write + one brush-changed
    // fan-out per card per frame). The panel owns the STATE, the theme owns the look.
    private void SyncLoadingState()
    {
        if (Owner is { } owner) owner.IsLoadingItems = _activeSkeletons.Count > 0;
    }

    private static void ArrangeSkeleton((UIComponent card, Rect rect) slot)
    {
        var m = (IMeasurableComponent)slot.card;
        m.Measure(new Size(slot.rect.Width, slot.rect.Height));
        m.Arrange(slot.rect);
    }

    // A card from the pool (already attached + pulsing) or a fresh one built from the theme's ItemSkeletonTemplate. The
    // panel owns no skeleton visual or animation - the card's whole look + breathe live in the template.
    private UIComponent RentSkeleton(DataTemplate template)
    {
        if (_skeletonPool.Count > 0)
        {
            var reused = _skeletonPool.Pop();
            reused.Visibility = Visibility.Visible;
            return reused;
        }
        var card = template.Build(this).RootComponent as UIComponent;
        if (card == null) return null;
        _skeletonSet.Add(card);
        AddVisualChild(card);
        return card;
    }

    private void RecycleSkeleton(int slot)
    {
        if (!_activeSkeletons.Remove(slot, out var card)) return;
        card.Visibility = Visibility.Collapsed;
        _skeletonPool.Push(card);
    }

    private void RecycleAllSkeletons()
    {
        foreach (var card in _activeSkeletons.Values)
        {
            card.Visibility = Visibility.Collapsed;
            _skeletonPool.Push(card);
        }
        _activeSkeletons.Clear();
        SyncLoadingState();   // no cards on screen -> the list is no longer loading -> the theme stops the shared pulse
    }

    // Drop all skeleton state - called from Revirtualize, which detaches every visual child (the cards among them), so
    // the pool/active/set would otherwise hand back cards that are no longer in the tree. Also forgets the item margin
    // (the ItemTemplate may have changed).
    private void ResetSkeletons()
    {
        // Clear the ACTIVE cards FIRST: that is what drops IsLoadingItems (SyncLoadingState) and so fires the theme's
        // ExitAction, which stops the shared pulse in the static AnimationManager. Dropping the references below without
        // it would leave the list reporting "loading" forever and the pulse ticking with no card on screen.
        RecycleAllSkeletons();
        _skeletonPool.Clear();
        _activeSkeletons.Clear();
        _skeletonSet.Clear();
        _pendingFrames = 0;
        _itemMarginKnown = false;
    }

    // A tab switch (or any subtree removal) detaches the panel while loading skeletons are still on screen. Detach alone
    // changes nothing about the list's loading state, so the theme's ExitAction would never fire and the shared pulse
    // would keep ticking in the static AnimationManager for a list nobody sees (the "switch tabs mid-load -> FPS never
    // recovers" report). Recycle the actives now to fire the stop; a re-attach + re-fill re-shows them and the pulse
    // restarts via the EnterAction.
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_activeSkeletons.Count > 0) RecycleAllSkeletons();
    }

    // The margin a real item's template leaves around each tile - read ONCE from a realized item so a skeleton card
    // lines up EXACTLY with the real tiles (never a hardcoded guess). Cached; ResetSkeletons re-reads it after a
    // regenerate, since a new ItemTemplate can have a different margin.
    private Thickness _itemMargin;
    private bool _itemMarginKnown;

    private Thickness ItemMargin()
    {
        if (_itemMarginKnown) return _itemMargin;
        var container = Owner?.ItemContainerGenerator.AnyRealizedContainer();
        if (container == null) return default;   // nothing realized yet - stay uncached, try again next frame
        _itemMargin = FindItemMargin(container);
        _itemMarginKnown = true;
        return _itemMargin;
    }

    // The item VISUAL (the ItemTemplate's root) carries the tile margin; the container/presenter chrome around it does
    // not. First descendant with a non-zero margin wins.
    private static Thickness FindItemMargin(IUIComponent node)
    {
        if (node is IMeasurableComponent m && !IsZero(m.EffectiveMargin)) return m.EffectiveMargin;
        foreach (var child in node.VisualChildren)
        {
            var found = FindItemMargin(child);
            if (!IsZero(found)) return found;
        }
        return default;
    }

    private static bool IsZero(Thickness t) => t.Left == 0 && t.Top == 0 && t.Right == 0 && t.Bottom == 0;

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
