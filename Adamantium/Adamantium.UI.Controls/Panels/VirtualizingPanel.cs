using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Animation;
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

    /// <summary>Milliseconds a single pass may spend (re)binding containers WHILE SCROLLING; whatever does not fit is
    /// deferred to the next pass and shows a skeleton. <b>0 means no budget</b> - bind the whole window in one pass.
    /// <para>A budget is legitimate HERE, unlike the general layout budget that was banned: the intake is bounded at the
    /// source (a pass can never want more than one window) and a deferred slot draws a skeleton, which is an honest
    /// placeholder rather than a stale rect. It is a dial rather than a constant because there is no right value for all
    /// windows: measured, 6 ms binds ~357 slots a pass and defers ~4487 on a big one, while a small one is better off
    /// binding everything at once.</para></summary>
    public static readonly AdamantiumProperty ScrollBindBudgetProperty = AdamantiumProperty.Register(nameof(ScrollBindBudget),
        typeof(double), typeof(VirtualizingPanel), new PropertyMetadata(6.0));

    public double ScrollBindBudget
    {
        get => GetValue<double>(ScrollBindBudgetProperty);
        set => SetValue(ScrollBindBudgetProperty, value);
    }

    /// <summary>Milliseconds a single pass may spend (re)binding when NOT scrolling - the initial fill or a settled
    /// fling, where the backlog should drain fast. **0 means no budget.** Larger than <see cref="ScrollBindBudget"/> by
    /// default because a still window can afford a longer pass without anyone seeing it.</summary>
    public static readonly AdamantiumProperty FillBindBudgetProperty = AdamantiumProperty.Register(nameof(FillBindBudget),
        typeof(double), typeof(VirtualizingPanel), new PropertyMetadata(30.0));

    public double FillBindBudget
    {
        get => GetValue<double>(FillBindBudgetProperty);
        set => SetValue(FillBindBudgetProperty, value);
    }

    /// <summary>The default floor, named so a caller can express "one guaranteed slice" without hard-coding the number.</summary>
    public const int MinBindsPerPassDefault = 8;

    /// <summary>The floor: however small the budget, a pass always (re)binds at least this many slots, so the window
    /// keeps filling instead of stalling on a machine where the very first bind already overruns.</summary>
    public static readonly AdamantiumProperty MinBindsPerPassProperty = AdamantiumProperty.Register(nameof(MinBindsPerPass),
        typeof(int), typeof(VirtualizingPanel), new PropertyMetadata(MinBindsPerPassDefault));

    public int MinBindsPerPass
    {
        get => GetValue<int>(MinBindsPerPassProperty);
        set => SetValue(MinBindsPerPassProperty, value);
    }

    /// <summary>The budget to hand <c>SetWindow</c>: the caller's milliseconds, with 0 meaning "no budget" spelled the
    /// way the generator understands it.</summary>
    protected static double BudgetOrUnlimited(double ms) => ms <= 0 ? double.MaxValue : ms;

    /// <summary>Index a dropped item would land at, or -1 (the default) for no drop in progress. A panel that honours it
    /// leaves a REAL empty slot there - items from that index on move along by one, so a wrapped line genuinely reflows
    /// instead of tiles sliding over each other - and fills the freed slot with the same skeleton card a not-yet-bound
    /// item gets. Layout stays the authority on where everything ends up; the motion between two of its answers is what
    /// gets animated, which is the only way a gap can open in a wrapping panel without lying about the result.</summary>
    public static readonly AdamantiumProperty DropGapIndexProperty = AdamantiumProperty.Register(nameof(DropGapIndex),
        typeof(int), typeof(VirtualizingPanel), new PropertyMetadata(-1, OnDropGapChanged));

    public int DropGapIndex
    {
        get => GetValue<int>(DropGapIndexProperty);
        set => SetValue(DropGapIndexProperty, value);
    }

    private static void OnDropGapChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        // The gap changes where every following item sits, so the slots have to be recomputed - but only when it MOVES,
        // which is a rare event during a drag (a few times a gesture), not something that runs per mouse move.
        if (a is VirtualizingPanel panel) panel.InvalidateMeasure();
    }

    /// <summary>The slot a drop at <paramref name="point"/> (in this panel's own coordinates) would land in, or false when
    /// the panel cannot say. It MUST NOT be derived from where the containers currently sit: the gap moves them, so an
    /// index read off them changes the gap, which moves them again - on a slot boundary that oscillates every frame. A
    /// panel with a regular grid answers from the grid itself, which the gap does not touch, so the answer is stable no
    /// matter what the gap is currently doing.</summary>
    public virtual bool TryGetDropSlot(Vector2 point, out int index)
    {
        index = -1;
        return false;
    }

    /// <summary>Whether this panel actually opens <see cref="DropGapIndex"/> in its layout. False (the default) means the
    /// drag shows its insertion caret instead - the two are alternatives, never both, since they mark the same place.</summary>
    public virtual bool SupportsDropGap => false;

    /// <summary>The slot an item at <paramref name="index"/> occupies once the drop gap is accounted for: everything from
    /// the gap on shifts along by one, opening exactly one item-sized hole. Identical to the index when no drop is in
    /// progress.</summary>
    protected int SlotOf(int index)
    {
        var gap = DropGapIndex;
        return gap >= 0 && index >= gap ? index + 1 : index;
    }

    /// <summary>How many slots the panel lays out: one more than the item count while a drop gap is open.</summary>
    protected int SlotCount(int itemCount) => DropGapIndex >= 0 ? itemCount + 1 : itemCount;

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

        Size desired;
        _inLayout = true;
        try
        {
            _offset = ClampOffset(_offset, _extent, _viewport);
            _passOffset = _offset;   // snapshot: the matching arrange positions against exactly this
            var extent = MeasureVirtualized(availableSize, _offset);
            _extent = extent;
            // An UNBOUNDED axis is a question ("how big would you like to be?"), not a statement that everything is
            // visible - a Grid star row probes its child unbounded to learn its natural size before resolving the row,
            // so this arrives every single pass. Reading it as a viewport collapses the scroll range to nothing, which
            // clamps the offset back to zero at the top of the NEXT measure: the list refuses to scroll and realizes
            // only its first window. Keep the viewport we already know on such an axis; only a bounded one updates it,
            // and only a never-measured axis falls back to the extent.
            _viewport = new Size(
                double.IsInfinity(availableSize.Width) ? (_viewport.Width > 0 ? _viewport.Width : extent.Width) : availableSize.Width,
                double.IsInfinity(availableSize.Height) ? (_viewport.Height > 0 ? _viewport.Height : extent.Height) : availableSize.Height);
            // The DESIRED size still answers that question honestly: the extent on an unbounded axis, the slot on a
            // bounded one. It is what the parent asked for and is independent of the viewport above.
            desired = new Size(
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
        return desired;
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
    /// <summary>Where item <paramref name="index"/> sits in this panel's own coordinates, whether or not it has been
    /// realized. The point of it: something scrolling TO an item that was virtualized away has no container to aim at,
    /// and the panel is the only one that knows where the item WOULD be. False when it cannot say.</summary>
    public virtual bool TryGetItemRect(int index, out Rect rect)
    {
        rect = default;
        return false;
    }

    /// <summary>How many containers virtualization has parked. A park is a Visibility write plus a binding walk over the
    /// container-s subtree, and a window that shrinks a lot does thousands at once - which is a render-cache structural
    /// frame, not a layout one. Counting them is what told a slider stutter apart from a layout cost.</summary>
    public static long ParkCalls;

    protected static void ParkContainer(IUIComponent container)
    {
        ParkCalls++;
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
        // This used to read EVERY child on EVERY arrange pass - measured at 130-205 thousand reads a second, finding
        // nothing across a 43-second run. A container only becomes a ghost when its index mapping is dropped, and the
        // generator knows exactly when that happens, so it records them and this drains the record: O(what changed).
        // The candidate set is a SUPERSET of the ghosts (every path that drops a mapping records it, every path that
        // takes one back removes it), so the three tests below still decide - the drain only says where to look.
        var generator = Owner.ItemContainerGenerator;
        var candidates = generator.DrainUnmapped();
        for (var i = 0; i < candidates.Count; i++)
        {
            var child = candidates[i];
            if (child.Visibility != Visibility.Visible)
            {
                continue;
            }

            if (_skeletonSet.Contains(child))
            {
                continue;   // panel-owned loading card, not a generator container
            }

            if (generator.IndexFromContainer(child) >= 0)
            {
                continue;   // back in the realized window - keep
            }

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
    // ONE card, drawn once per pending slot through RenderClones (§4o) - not one card per slot. Building a full template
    // instance per slot is what this replaces: measured on a tile-size drag, 3469 template builds in a 0.25 s window
    // against 147 realized containers, and every property write of every build marked layout dirty (measure=15952,
    // arrange=44949, frame down to 30 fps). The clones live only in the instance buffer: no layout, no hit-test, no state.
    private UIComponent _skeletonPrototype;
    private Size _prototypeSize;
    private List<Matrix4x4F> _skeletonClones;   // fresh list per change - the draw walk may read it off the render thread

    /// <summary>How many skeleton cards are on screen right now - clones of the one prototype.</summary>
    protected int ActiveSkeletonCount => _skeletonClones?.Count ?? 0;
    private readonly HashSet<IUIComponent> _skeletonSet = new();             // panel-owned visuals (skip in HideUnmappedContainers)

    /// <summary>Shows a loading placeholder at each of the generator's budget-deferred slots. ONE themed
    /// <c>ItemSkeletonTemplate</c> card is built, measured and arranged - every slot is a CLONE of it
    /// (<see cref="IUIComponent.RenderClones"/>), which exists only in the instance buffer. <paramref name="slotRect"/>
    /// maps a slot index to its absolute grid rect; the subclass owns that geometry and calls this from
    /// ArrangeVirtualized. O(pending) matrix writes, and not one element per slot.</summary>
    protected void ReconcileSkeletons(Func<int, Rect> slotRect)
    {
        var pending = Owner.ItemContainerGenerator.PendingIndices;

        if (pending.Count == 0)
        {
            HideSkeletons();
            return;
        }

        // NO delay. There used to be one - six FRAMES, on the reasoning that at 60 fps it is ~100ms and stops cards
        // flashing on a fill that clears immediately. But a fill is exactly when frames are slow, so those six frames
        // ran to half a second on a heavy tab and the window looked hung: the heuristic held the placeholder back
        // hardest in the case it exists for. A deferred slot is a hole on screen; showing it at once is the honest
        // answer, and the cheap one (every card is a CLONE of one prototype - see below).
        var template = Owner?.ItemSkeletonTemplate;
        if (template == null) return;   // unthemed ItemsControl - no skeletons

        var prototype = EnsureSkeletonPrototype(template);
        if (prototype == null) return;   // template has no root

        // Inset each cell rect by the REAL tile's margin (read from a realized item, never hardcoded) so a card sits
        // exactly where its item's visual would - same footprint, same inter-tile gaps.
        var inset = ItemMargin();
        var firstRect = slotRect(pending[0]);
        var size = new Size(
            Math.Max(0, firstRect.Width - inset.Left - inset.Right),
            Math.Max(0, firstRect.Height - inset.Top - inset.Bottom));

        // Arranged ONCE, at the ORIGIN: the clones carry the positions. Slots of a virtualizing grid are the same size
        // by construction, so one arranged card fits them all - which is exactly why a translation is enough and no
        // clone needs a scale (a scale would stretch the card's border thickness with it).
        if (prototype.Visibility != Visibility.Visible) prototype.Visibility = Visibility.Visible;
        if (_prototypeSize != size)
        {
            var measurable = (IMeasurableComponent)prototype;
            measurable.Measure(size);
            measurable.Arrange(new Rect(0, 0, size.Width, size.Height));
            _prototypeSize = size;
        }

        // A FRESH list each time: the draw walk reads RenderClones on the render thread, so refilling the live one in
        // place would move clones under the frame drawing them.
        var clones = new List<Matrix4x4F>(pending.Count);
        for (var i = 0; i < pending.Count; i++) AddClone(clones, slotRect(pending[i]), inset);

        // A slot leaves PendingIndices the moment it gets a CONTAINER - which is one or more passes before that container
        // is arranged and drawn. Dropping its card then opens a hole with neither tile nor skeleton, and a streaming fill
        // shows it as a band along the realize frontier. (The old per-slot cards hid this by accident: they were dismissed
        // with Visibility=Collapsed, which only takes effect on a later pass, so a card lingered exactly long enough.)
        // The honest rule is not a delay but a condition: a skeleton stands until its slot is actually LAID OUT.
        foreach (var index in Owner.ItemContainerGenerator.RealizedIndices)
        {
            if (Owner.ItemContainerGenerator.ContainerFromIndex(index) is IMeasurableComponent { IsArrangeValid: true }) continue;
            AddClone(clones, slotRect(SlotOf(index)), inset);
        }

        // Only when the set actually MOVED. The prototype's re-record is what carries the new set to the render side, and
        // a component re-recorded every pass keeps its window off the clean-frame fast path for as long as skeletons are
        // up - measured at 600 fps -> 180 when this was unconditional. A steady screenful of pending slots produces the
        // same matrices frame after frame, so the common case is no mark at all.
        if (!SameClones(_skeletonClones, clones))
        {
            _skeletonClones = clones;
            prototype.RenderClones = clones;

            // The recorder walks the geometry-dirty QUEUE, not the IsGeometryValid flag: InvalidateRender only clears the
            // flag, so without the mark the prototype was recorded once at birth (clones still null) and never again -
            // the group kept an empty set for good and the card drew once at the origin.
            prototype.InvalidateRender(false);
            RenderDirty.MarkGeometry(prototype);
        }

        SyncLoadingState();
    }

    private static bool SameClones(List<Matrix4x4F> a, List<Matrix4x4F> b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a == null || b == null || a.Count != b.Count) return false;

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i]) return false;
        }

        return true;
    }

    private static void AddClone(List<Matrix4x4F> clones, Rect slot, Thickness inset) =>
        clones.Add(Matrix4x4F.Translation((float)(slot.X + inset.Left), (float)(slot.Y + inset.Top), 0f));

    // The one card every slot is a clone of. Built from the theme's template; the panel owns no skeleton visual or
    // animation of its own - the whole look and breathe live in the template.
    private UIComponent EnsureSkeletonPrototype(DataTemplate template)
    {
        if (_skeletonPrototype != null) return _skeletonPrototype;

        _skeletonPrototype = template.Build(this).RootComponent as UIComponent;
        if (_skeletonPrototype == null) return null;

        _skeletonSet.Add(_skeletonPrototype);   // panel-owned, not a generator container
        AddVisualChild(_skeletonPrototype);
        return _skeletonPrototype;
    }

    private void HideSkeletons()
    {
        if (_skeletonClones == null && _skeletonPrototype == null) return;

        _skeletonClones = null;
        if (_skeletonPrototype != null)
        {
            _skeletonPrototype.RenderClones = null;
            _skeletonPrototype.Visibility = Visibility.Collapsed;
        }

        SyncLoadingState();
    }

    // The LIST-level loading state (ItemsControl.IsLoadingItems): true exactly while cards are on screen. The theme keys
    // the skeleton shimmer off it - ONE trigger per list starts/stops the pulse on the shared skeleton brush - so a
    // screenful of cards costs one animation, not one per card (which is also one property write + one brush-changed
    // fan-out per card per frame). The panel owns the STATE, the theme owns the look.
    private void SyncLoadingState()
    {
        if (Owner is { } owner) owner.IsLoadingItems = ActiveSkeletonCount > 0;
    }

    private static void ArrangeSkeletonUnused((UIComponent card, Rect rect) slot)
    {
        var m = (IMeasurableComponent)slot.card;
        m.Measure(new Size(slot.rect.Width, slot.rect.Height));
        m.Arrange(slot.rect);
    }

    // A card from the pool (already attached + pulsing) or a fresh one built from the theme's ItemSkeletonTemplate. The
    // panel owns no skeleton visual or animation - the card's whole look + breathe live in the template.
    // Where each ITEM last sat. Keyed by the DATA ITEM, never by the container: containers are recycled onto other items
    // as the window scrolls, so "where this container was last time" is a different item's position and would fling
    // tiles in from wherever the recycled container came from.
    private readonly Dictionary<object, Vector2> _lastItemPos = new();
    private int _animatedGap = -1;   // the gap the last animated pass ran for
    private static readonly TimeSpan LayoutMoveDuration = TimeSpan.FromSeconds(0.18);

    /// <summary>Animates the tiles that layout just MOVED, from where they were to where they now are. Layout stays the
    /// authority - it has already put everything in its final place, including a line that reflowed around the drop gap -
    /// and this only interpolates the difference on the render transform, which is why a wrapping panel can open a hole
    /// without tiles sliding over each other. Nothing animates the first time an item is seen (it has no previous place),
    /// and scrolling moves nothing, since the grid is absolute and an item's slot does not change when the view does.</summary>
    // Rows promoted for the duration of an open gap. A promotion costs a transform slot, so it is not left behind: the
    // moment the gap closes they all go back to being ordinary children.
    private readonly HashSet<UIComponent> _promoted = new();

    /// <summary>Makes a row carry its own subtree (or stop). The snapshot records this, and the draw side bakes the
    /// subtree node-relative or world-baked accordingly - so a change nobody announced leaves the two disagreeing about
    /// where everything is. Idempotent, which is what keeps it cheap to call every arrange.</summary>
    private static void AsMotionNode(UIComponent row, bool on)
    {
        if (row.IsRenderMotionNode == on) return;

        row.IsRenderMotionNode = on;
        RenderDirty.MarkTransform(row);
        RenderDirty.MarkSubtreeGeometry(row);   // its subtree is baked in a DIFFERENT space now - re-take the record
    }

    protected void AnimateLayoutMoves(Func<int, Rect> slotRect)
    {
        // Gap closed: nothing is travelling any more, so give the slots back.
        if (DropGapIndex < 0 && _promoted.Count > 0)
        {
            foreach (var row in _promoted) AsMotionNode(row, false);
            _promoted.Clear();
        }

        var items = Owner?.Items;
        if (items == null) return;

        // Only a MOVED DROP GAP animates. Layout moves tiles for other reasons too - a resize reflows the whole grid, a
        // narrower window changes the column count - and sliding hundreds of tiles into place then is both expensive and
        // visually noisy. Those passes still record where everything ended up, so the next gap move measures from the
        // truth rather than from a position two layouts old.
        var animate = DropGapIndex != _animatedGap;
        _animatedGap = DropGapIndex;

        foreach (var index in Owner.ItemContainerGenerator.RealizedIndices)
        {
            if (index < 0 || index >= items.Count) continue;
            var item = items[index];
            if (item == null) continue;

            var now = slotRect(SlotOf(index)).Location;
            var moved = _lastItemPos.TryGetValue(item, out var before) && before != now;
            _lastItemPos[item] = now;
            if (!moved) continue;

            if (Owner.ItemContainerGenerator.ContainerFromIndex(index) is not UIComponent container) continue;

            // A ROW TRAVELS AS ONE THING. Promoted to a motion node, its subtree is baked relative to IT and the shader
            // applies its slot matrix to everything beneath - so the parts that live only in a shared-mesh arena travel
            // with it. Without this the row rode an ANCESTOR's slot: the row's own movement never reached its drag grip,
            // and the grip stayed where the row had been while the rest of it left. Measured at 23 px apart mid-slide.
            AsMotionNode(container, true);
            _promoted.Add(container);

            // ...and the record still has to be re-taken, because a gap shift moves only the rows BELOW it and one
            // matrix cannot say that: a slot moves the whole list or none of it.
            RenderDirty.MarkSubtreeGeometry(container);

            if (!animate) continue;
            if (container.RenderTransform is not { } transform)
            {
                transform = new Transform();
                container.RenderTransform = transform;
            }

            // Start where it WAS and run to zero: the element is already arranged at its new place, so the transform only
            // carries the leftover distance. FillBehavior.Stop leaves nothing behind once it lands.
            transform.BeginAnimation(Transform.TranslateXProperty,
                new DoubleAnimation { From = before.X - now.X, To = 0, Duration = LayoutMoveDuration, FillBehavior = FillBehavior.Stop });
            transform.BeginAnimation(Transform.TranslateYProperty,
                new DoubleAnimation { From = before.Y - now.Y, To = 0, Duration = LayoutMoveDuration, FillBehavior = FillBehavior.Stop });
        }
    }

    private UIComponent _gapCard;   // the drop placeholder; separate from the skeleton pool ON PURPOSE - see below

    /// <summary>Puts the drop PLACEHOLDER in the open gap - the card that says "what you are holding lands here".
    /// Deliberately NOT a loading skeleton, and kept out of the skeleton pool and out of <c>IsLoadingItems</c>: a
    /// skeleton means "content is on its way", it pulses the whole list while it is up, and it waits several frames
    /// before appearing so a quick fill does not flash. All three are wrong for a placeholder that has to appear under
    /// the cursor at once and must not tell the user about work that is not happening. Called from ArrangeVirtualized;
    /// <paramref name="slotRect"/> is the same geometry the tiles use.</summary>
    protected void ReconcileDropPlaceholder(Func<int, Rect> slotRect)
    {
        var gap = DropGapIndex;
        var template = Owner?.DropPlaceholderTemplate;
        if (gap < 0 || template == null)
        {
            if (_gapCard != null) _gapCard.Visibility = Visibility.Collapsed;
            return;
        }

        if (_gapCard == null)
        {
            _gapCard = template.Build(this).RootComponent as UIComponent;
            if (_gapCard == null) return;
            _skeletonSet.Add(_gapCard);   // same "not a container" exemption the skeleton prototype gets
            AddVisualChild(_gapCard);
        }

        _gapCard.Visibility = Visibility.Visible;
        var rect = slotRect(gap).Deflate(ItemMargin());
        var measurable = (IMeasurableComponent)_gapCard;
        measurable.Measure(new Size(rect.Width, rect.Height));
        measurable.Arrange(rect);
    }

    // Drop all skeleton state - called from Revirtualize, which detaches every visual child (the prototype among them),
    // so a stale reference would otherwise point at a card no longer in the tree. Also forgets the item margin (the
    // ItemTemplate may have changed).
    private void ResetSkeletons()
    {
        // Hide FIRST: that is what drops IsLoadingItems (SyncLoadingState) and so fires the theme's ExitAction, which
        // stops the shared pulse in the static AnimationManager. Dropping the reference below without it would leave the
        // list reporting "loading" forever and the pulse ticking with no card on screen.
        HideSkeletons();
        _skeletonPrototype = null;
        _prototypeSize = default;
        _skeletonSet.Clear();
        _itemMarginKnown = false;
    }

    // A tab switch (or any subtree removal) detaches the panel while loading skeletons are still on screen. Detach alone
    // changes nothing about the list's loading state, so the theme's ExitAction would never fire and the shared pulse
    // would keep ticking in the static AnimationManager for a list nobody sees (the "switch tabs mid-load -> FPS never
    // recovers" report). Hide now to fire the stop; a re-attach + re-fill re-shows them and the pulse restarts.
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        HideSkeletons();
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
