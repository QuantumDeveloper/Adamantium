using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Diagnostics;

namespace Adamantium.UI.Core;

/// <summary>
/// Owns the per-frame layout pass for one visual-tree root (a window / top-level). It is the single driver of
/// style-application + measure + arrange, replacing the old per-component full-tree walk that lived in WindowExtension.
/// </summary>
/// <remarks>
/// Phase 1 of the layout-manager plan (docs/LAYOUT_MANAGER_PLAN.md): the full per-frame DFS walk is gone. Invalidation
/// (<see cref="IMeasurableComponent.InvalidateMeasure"/> / <see cref="IMeasurableComponent.InvalidateArrange"/> /
/// <see cref="IFundamentalUIComponent.InvalidateStyles"/>) now registers the affected node in this manager's dirty
/// queues; <see cref="ExecuteLayoutPass"/> drains only those queues. A clean frame (nothing invalid) walks nothing.
///
/// One manager per root, kept persistently (so invalidations that happen BETWEEN passes accumulate). A node finds its
/// manager by walking up to its top-most visual ancestor (<see cref="For"/>); every entry point (the window's per-frame
/// update and the tests' UpdateTree) is invoked with that same top-most node, so both resolve to the same instance.
///
/// Later phases make arrange strictly top-down (drop the parent.DesiredSize fallback) and remove the re-entrancy
/// crutches - all behind this same entry point.
/// </remarks>
public sealed class LayoutManager
{
    // Persistent per-root managers, keyed (weakly) by the top-most visual node so they are GC'd with their tree.
    private static readonly ConditionalWeakTable<IUIComponent, LayoutManager> Managers = new();

    // Guards against a pathological re-dirtying loop (a node that invalidates itself every time it's laid out): the
    // outer drain loop bails after this many iterations rather than spinning forever.
    private const int MaxPassIterations = 100;

    // F1 layout time-budgeting: max wall-time one ExecuteLayoutPass may spend before deferring the rest to the next
    // frame. Default 8 ms - high enough that a normal frame finishes well within it, low enough to keep a pathological
    // frame (binding storm / view switch) responsive; deferred subtrees keep their last Bounds and render in place
    // until they catch up. Set to null to disable (always finish the pass in one go).
    public static TimeSpan? FrameBudget = TimeSpan.FromMilliseconds(8);

    private readonly IUIComponent _root;
    private readonly DirtyQueue _toStyle = new();
    private readonly DirtyQueue _toMeasure = new();
    private readonly DirtyQueue _toArrange = new();
    // Nodes that asked to be re-measured on the NEXT pass, not this one. A virtualizing panel realizing a large window in
    // per-frame slices re-queues itself here so the continuation lands next frame - if it enqueued into _toMeasure the
    // drain loop below would process it in THIS same pass, realizing the whole window in one frame (the very burst we are
    // spreading). Promoted into the real measure queue at the start of each pass.
    private readonly HashSet<IUIComponent> _toMeasureNextPass = new();
    private readonly List<IUIComponent> _passBuffer = new();   // reused snapshot buffer for one phase's drain
    private readonly List<IUIComponent> _offscreenBuffer = new();   // scratch for the visible-first partition
    private readonly List<IUIComponent> _promoteBuffer = new();   // reused scratch for promoting next-pass deferrals
    private readonly System.Diagnostics.Stopwatch _passStopwatch = new();   // reused per pass (no per-frame allocation)
    private int _deferredStreak;   // consecutive budget-capped passes that didn't fully drain (anti-starvation)
    private const int MaxDeferredPasses = 4;   // after this many, drop the budget for one pass to clear the backlog

    public LayoutManager(IUIComponent root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    /// <summary>Gets (creating once) the manager that owns the given root. The root is whatever top-level node the
    /// layout pass is driven from (a window at runtime, an arbitrary subtree root in tests).</summary>
    public static LayoutManager GetOrCreate(IUIComponent root) => Managers.GetValue(root, static r => new LayoutManager(r));

    /// <summary>Resolves the manager responsible for <paramref name="node"/> by walking up to its top-most visual
    /// ancestor and getting (or creating) that root's manager.</summary>
    public static LayoutManager For(IUIComponent node)
    {
        // The node's root visual is already cached and kept current by the attach/detach walk (SetVisualParent propagates
        // RootVisual through the WHOLE subtree on both attach and detach), so read it directly - O(1) - instead of walking
        // to the top on every call. For() is on the layout invalidation hot path: every InvalidateMeasure/InvalidateArrange
        // resolves the owning manager through here. A not-yet-attached subtree (or a plain non-root test tree) has
        // RootVisual == null; fall back to the walk so its local top still owns a manager - the same key the layout pass is
        // driven from (GetOrCreate(window/root)), so the resolved manager is identical either way.
        var root = node.RootVisual;
        if (root != null)
        {
            return GetOrCreate(root);
        }

        var top = node;
        while (top.VisualParent != null)
        {
            top = top.VisualParent;
        }
        return GetOrCreate(top);
    }

    // Every invalidation means the loop owes another layout pass - so it must not be asleep. Layout is NOT covered by the
    // render-dirty marks (a measure/arrange is not a render mark), so without this the loop only woke on its 250 ms safety
    // timeout: a tab's content, which is built and laid out over several passes, then crawled in at four passes a second and
    // looked simply blank. See LoopSignal.
    public void InvalidateStyle(IUIComponent node)
    {
        _toStyle.Enqueue(node);
        LoopSignal.Request();
    }

    public void InvalidateMeasure(IUIComponent node)
    {
        LoopSignal.Request();
        // Parallel-rebind window (a virtualizing panel is preparing+measuring its tiles across threads): a rebind's
        // synchronous binding writes flip AffectsMeasure props -> InvalidateMeasure, which would concurrently mutate this
        // root's DirtyQueue (PriorityQueue + HashSet, NOT thread-safe). COLLECT lock-free and REPLAY sequentially when the
        // pass ends (BeginDeferredInvalidation/EndDeferredInvalidation) - deferred write, no locks on the layout hot path.
        if (_deferInvalidations) { DeferredMeasure.Enqueue(node); return; }
        // A measure-invalid node also needs re-arranging; enqueue it for both so the arrange phase re-runs after the
        // measure phase recomputes sizes.
        _toMeasure.Enqueue(node);
        _toArrange.Enqueue(node);
    }

    public void InvalidateArrange(IUIComponent node)
    {
        LoopSignal.Request();
        if (_deferInvalidations) { DeferredArrange.Enqueue(node); return; }
        _toArrange.Enqueue(node);
    }

    // ---- Parallel-rebind deferred invalidation ---------------------------------------------------------------------
    // A virtualizing panel rebinds+measures its tiles across cores (each tile is a disjoint subtree, so its own
    // component-flag writes are isolated); the ONLY escape is the shared per-root DirtyQueue enqueue above. While the
    // flag is set, InvalidateMeasure/Arrange route into these lock-free concurrent queues instead; EndDeferredInvalidation
    // replays them through the normal path on the (single) coordinating thread once the parallel pass has joined.
    // Static: the flag is toggled around a Parallel.ForEach (a full fork/join barrier, so the write is visible to workers
    // and their enqueues are visible back), and only worker threads run in between, so a single global switch is enough.
    private static volatile bool _deferInvalidations;
    private static readonly System.Collections.Concurrent.ConcurrentQueue<IUIComponent> DeferredMeasure = new();
    private static readonly System.Collections.Concurrent.ConcurrentQueue<IUIComponent> DeferredArrange = new();

    public static void BeginDeferredInvalidation() => _deferInvalidations = true;

    public static void EndDeferredInvalidation()
    {
        _deferInvalidations = false;
        while (DeferredMeasure.TryDequeue(out var n)) For(n).InvalidateMeasure(n);
        while (DeferredArrange.TryDequeue(out var n)) For(n).InvalidateArrange(n);
    }

    /// <summary>Requests that <paramref name="node"/> be re-measured on the NEXT pass rather than this one. Used by a
    /// virtualizing panel that realized only a slice of a large window this frame and wants to continue next frame (so a
    /// big realize burst is spread over frames instead of hanging one). Safe to call mid-pass: it does not touch this
    /// pass's queues.</summary>
    // A budget-deferred continuation (a virtualizing panel realizing its window in slices). Nothing will EVENT the loop back -
    // the work is already known - so it must signal, or the fill stalls until the safety timeout.
    public void InvalidateMeasureNextPass(IUIComponent node)
    {
        _toMeasureNextPass.Add(node);
        LoopSignal.Request();
    }

    /// <summary>Raised once at the end of a layout pass that actually did work (queues drained), i.e. when layout has
    /// settled for this frame. Not raised on a clean frame - so a consumer (e.g. the render cache) can rebuild on this
    /// signal instead of every frame.</summary>
    public event EventHandler LayoutUpdated;

    /// <summary>
    /// Runs one layout pass: drain the style queue (apply themes - this can change templates, so it must precede
    /// measure), then the measure queue, then the arrange queue, ancestors-first within each. Re-dirtying during the
    /// pass (e.g. a measure that invalidates an arrange) is handled by looping until all queues drain.
    /// </summary>
    public void ExecuteLayoutPass()
    {
        // F2: apply this frame's batched binding updates (coalesced) BEFORE laying out, so the target writes and the
        // measure/arrange invalidations they trigger are drained by this same pass. The global queue flushes once per
        // frame: the first root's pass empties it, later roots find it empty.
        BindingUpdateQueue.Flush();

        // Promote nodes that deferred their re-measure to this pass (a virtualizing panel continuing a sliced realize).
        // Snapshot + clear first: a promoted node re-measures later in this pass's drain and may re-defer itself for the
        // NEXT pass, which must land in the now-empty set rather than being wiped. InvalidateMeasure (not a bare enqueue)
        // so the validity flag is actually cleared - a bare enqueue would be gate-skipped by MeasureDirty. _inLayout is
        // false here (pass start), so a panel's muted-invalidation override lets this through.
        if (_toMeasureNextPass.Count > 0)
        {
            _promoteBuffer.Clear();
            foreach (var node in _toMeasureNextPass) _promoteBuffer.Add(node);   // struct enumerator, no boxing/heap alloc
            _toMeasureNextPass.Clear();
            foreach (var node in _promoteBuffer)
                if (node is IMeasurableComponent measurable) measurable.InvalidateMeasure();
        }

        // Forward-progress safety net: if the root itself is dirty but was never enqueued (e.g. it was invalidated
        // during construction, before this manager existed / before the subtree was assembled under it), seed it now.
        // This is O(1) - two flag reads on the root - not a tree walk, so a clean frame still costs nothing.
        if (_root is IMeasurableComponent rootMeasurable)
        {
            if (!rootMeasurable.IsMeasureValid) InvalidateMeasure(_root);
            else if (!rootMeasurable.IsArrangeValid) _toArrange.Enqueue(_root);
        }

        // F1: optional per-frame time budget. Null = unlimited (drain everything - the default, behaviour-neutral). When
        // set, the pass processes dirty nodes until the budget is spent, then defers the rest to the next frame - a
        // deferred subtree keeps its last Bounds and renders in place (graceful lag, not a hole).
        // Anti-starvation: if the queues have stayed un-drained for too many budget-capped passes, drop the budget for
        // THIS pass and drain fully - one slower frame clears the backlog instead of it growing forever.
        var budget = FrameBudget;
        if (budget.HasValue && _deferredStreak >= MaxDeferredPasses) budget = null;
        _passStopwatch.Restart();   // always time the pass (used for the budget when set, and for RuntimeStats either way)
        // Visible-first prioritisation only matters when the budget can actually defer work; skip its cost otherwise.
        var viewport = budget.HasValue ? Viewport : null;

        var didWork = false;
        var iterations = 0;
        var withinBudget = true;
        while (withinBudget && (!_toStyle.IsEmpty || !_toMeasure.IsEmpty || !_toArrange.IsEmpty))
        {
            if (++iterations > MaxPassIterations)
            {
                if (LayoutTrace.Enabled) LayoutTrace.Log($"LAYOUT PASS aborted after {MaxPassIterations} iterations (re-dirty loop)");
                break;
            }
            didWork = true;

            // Drain each queue as a SNAPSHOT (process only what's queued now), ordered style -> measure -> arrange. Work
            // re-dirtied DURING a phase lands back in the queues and is handled on the NEXT iteration - keeping the
            // phases ordered and letting re-entrant invalidation converge.
            var styleOk = DrainPhase(_toStyle, ApplyTheme, budget, viewport);
            var measureOk = styleOk && DrainPhase(_toMeasure, MeasureDirty, budget, viewport);
            // The ARRANGE phase must RUN even when style/measure exhausted the budget - short-circuiting it here tore the
            // frame: a virtualizing panel's measure REBINDS containers to new window slots, so until its arrange runs the
            // rebound tiles still sit at their PREVIOUS Bounds (drawn scattered over/between the new rows) and the newly
            // deferred slots have no skeletons (ReconcileSkeletons lives in arrange) - the fast-scroll "holes with nothing
            // in them" + the resize overlap, self-healing a frame later when arrange finally ran. DrainPhase's own
            // on-screen protection still applies (its visible prefix always completes; only the off-screen tail defers) and
            // ArrangeDirty re-queues any node whose measure was itself deferred, so this can't pull deferred measure work in.
            var arrangeOk = DrainPhase(_toArrange, ArrangeDirty, budget, viewport);
            withinBudget = measureOk && arrangeOk;
        }

        var settled = _toStyle.IsEmpty && _toMeasure.IsEmpty && _toArrange.IsEmpty;
        _deferredStreak = settled ? 0 : _deferredStreak + 1;

        // A pass that found NOTHING to do (queues empty at start, nothing promoted) is the "swap has settled" signal for
        // RenderDirty.ForceStructuralUntilSettled (theme/DPI): every settle write flows through this pass (binding flush,
        // style/measure/arrange drains, the resource flush below), so a workless pass means the cascade is done - and the
        // frame's render build, which runs AFTER this, still does one final forced walk to pick up the tail.
        if (!didWork) RenderDirty.NotifyLayoutQuiescent();

        RuntimeStats.LastLayoutPassMs = _passStopwatch.Elapsed.TotalMilliseconds;
        RuntimeStats.LastPassBudgetDeferred = !settled;

        // LayoutUpdated = "layout settled this frame" - only when everything drained, not when budget-deferred.
        if (didWork && settled)
            LayoutUpdated?.Invoke(this, EventArgs.Empty);

        // Coalesced resource-change notification: the style drain above may have loaded a new theme's dictionaries (a
        // theme swap re-themes via the style queue), so fire ResourcesChanged HERE, once, after they're present - live
        // {ObservableResource}s then re-resolve to the new values. No-op (a flag read) when nothing changed.
        UIAppContext.Current?.ResourceManager?.FlushResourceChanges();
    }

    // Drains one queue as a snapshot, ancestors-first. Returns true if it emptied the snapshot, false if it stopped on
    // the frame budget (re-queuing the unprocessed tail for the next frame). Always processes at least one node, so a
    // tiny/zero budget still makes forward progress. Under a budget, on-screen nodes are processed first so it's the
    // off-screen work that gets deferred.
    private bool DrainPhase(DirtyQueue queue, Action<IUIComponent> process, TimeSpan? budget, Rect? viewport)
    {
        queue.DrainInto(_passBuffer);
        var count = _passBuffer.Count;

        // On-screen work is NEVER budget-deferred - only OFF-screen work (which isn't drawn this frame) may spill to the
        // next frame. Deferring an on-screen node renders it BEFORE it is laid out: a freshly-realized container piles at
        // the origin (the resize "garble"), and a cohesive control tears (a slider's thumb, positioned by this arrange,
        // lags its fill which was set immediately on the value change). So partition the buffer visible-first and let the
        // budget cut in only PAST the on-screen prefix. With no budget or no viewport, everything is protected (drain
        // fully) - the safe default (a frame with no known client area can't tell what's visible, so it never defers).
        var protectedCount = count;
        if (budget.HasValue && viewport.HasValue && count > 1)
            protectedCount = PrioritizeVisibleFirst(_passBuffer, viewport.Value);

        for (var i = 0; i < count; i++)
        {
            process(_passBuffer[i]);
            if (budget.HasValue && i + 1 >= protectedCount && i + 1 < count && _passStopwatch.Elapsed >= budget.Value)
            {
                for (var j = i + 1; j < count; j++) queue.Enqueue(_passBuffer[j]);
                return false;
            }
        }
        return true;
    }

    // The root's on-screen area (world coords), or null if the root has no client size (e.g. an unsized test root).
    private Rect? Viewport => _root is IRootVisualComponent r && r.ClientWidth > 0 && r.ClientHeight > 0
        ? new Rect(0, 0, r.ClientWidth, r.ClientHeight)
        : null;

    // Stable-partition the buffer so on-screen nodes come first (each group keeps its ancestors-first depth order), so
    // under the budget the visible work is done first and the off-screen work is what gets deferred. Returns the number
    // of on-screen nodes now at the front - the "protected" prefix the budget must not defer.
    private int PrioritizeVisibleFirst(List<IUIComponent> buffer, Rect viewport)
    {
        _offscreenBuffer.Clear();
        var write = 0;
        for (var read = 0; read < buffer.Count; read++)
        {
            if (IsOnScreen(buffer[read], viewport)) buffer[write++] = buffer[read];
            else _offscreenBuffer.Add(buffer[read]);
        }
        var visibleCount = write;
        for (var k = 0; k < _offscreenBuffer.Count; k++) buffer[write++] = _offscreenBuffer[k];
        return visibleCount;
    }

    private static bool IsOnScreen(IUIComponent node, Rect viewport)
    {
        var b = node.Bounds;
        if (b.Width <= 0 || b.Height <= 0) return true;   // unsized / brand-new -> don't deprioritise it
        // WorldTransform ALREADY carries this node's own offset (LocalTransform = Translation(Bounds.Location)), so map a
        // LOCAL-origin rect - using Bounds (whose Location is that same offset) double-counts it and pushes on-screen
        // nodes off-screen, so the budget wrongly deferred VISIBLE work (a cause of the resize garble). Mirrors the render
        // path, which transforms new Rect(0,0,RenderSize) by the world matrix.
        var w = new Rect(0, 0, b.Width, b.Height).TransformToAABB(node.WorldTransform);
        return w.X < viewport.X + viewport.Width && w.X + w.Width > viewport.X
            && w.Y < viewport.Y + viewport.Height && w.Y + w.Height > viewport.Y;
    }

    private static void ApplyTheme(IUIComponent node)
    {
        if (node is FundamentalUIComponent { IsStyleApplied: false } themed)
        {
            themed.ApplyCurrentTheme();
        }
    }

    private static void MeasureDirty(IUIComponent node)
    {
        var control = (IMeasurableComponent)node;
        if (control.IsMeasureValid) return;   // already measured this pass via an ancestor's cascade

        if (LayoutTrace.Enabled)
        {
            var name = string.IsNullOrEmpty(node.Name) ? node.GetType().Name : node.Name;
            LayoutTrace.Log($"MEASURE-DIRTY {name}");
        }

        var before = control.DesiredSize;

        // Re-measure with the element's OWN cached constraint (the available size the parent last gave it), NOT a guess.
        // MeasureOverride cascades down, the validity gate skipping any clean subtree. A root visual (window or any
        // IRootVisualComponent top-level) uses its client size; a never-measured top-most node falls back to its
        // Width/Height (-> Infinity if auto).
        if (node is IRootVisualComponent root)
        {
            MeasureControl(control, root.ClientWidth, root.ClientHeight);
        }
        else if (control.PreviousMeasureConstraint is { } cached)
        {
            control.Measure(cached);
        }
        else
        {
            MeasureControl(control, control.Width, control.Height);
        }

        // Finer propagation: a parent's measure depends on this child only if the child's OUTWARD size changed. If it
        // did, invalidate the parent (re-measured next iteration, gate-skipping this now-clean child); if it did not,
        // the re-measure stayed contained in this subtree - no walk up to the window.
        // EXCEPT a parent that is a MEASURE BOUNDARY (its DesiredSize can't change from a child - a virtualizing items
        // host whose extent is count×cell, or any element with a fixed Width+Height): propagating into it here is
        // spurious and, because this runs on the queue-drain path OUTSIDE the panel's own _inLayout mute, it re-dirties
        // the panel every iteration and spins the pass to MaxPassIterations - the whole realize backlog draining in one
        // pass instead of one slice per frame. Honor the boundary here too, so InvalidateMeasureNextPass is respected.
        if (control.DesiredSize != before
            && node.VisualParent is IMeasurableComponent { IsMeasureValid: true } parent
            && !parent.IsMeasureBoundary)
        {
            parent.InvalidateMeasure();
        }
    }

    private void ArrangeDirty(IUIComponent node)
    {
        var control = (IMeasurableComponent)node;
        if (control.IsArrangeValid) return;
        if (!control.IsMeasureValid)
        {
            // Its measure was re-dirtied (re-entrancy) after this arrange entry was queued; defer the arrange to a later
            // iteration, after the measure queue re-drains. (Re-enqueue rather than drop, or the node stays unarranged.)
            _toArrange.Enqueue(node);
            return;
        }

        // Arrange the node into its OWN last correct slot (preserved across invalidation), NOT parent.DesiredSize - that
        // old fallback parked a dirty child at its parent's origin (the "pile at (0,0)" bug, plan problem #3). The node's
        // ArrangeOverride then re-distributes correct rects to its children. Only a node that was never arranged (the
        // root, on first layout) has no saved slot -> it fills its own measured area.
        //
        // The root visual is the exception: its slot is its CURRENT client area, never a saved slot. On a resize (e.g.
        // maximize) the client size grows and the root re-measures to it (MeasureDirty uses ClientWidth), but its
        // PreviousArrangeSlot still holds the OLD rect. ArrangeCore force-sets the root's RenderSize to DesiredSize (so
        // Bounds looked correct), yet the alignment clamp re-derives ClipRectangle from finalRect - so the stale slot
        // pinned the window's clip (and thus root-level hit-testing) to the old size while every child grew, killing
        // hovers/scroll in the newly-exposed area. Feed the live client rect so measure and arrange agree.
        var slot = node is IRootVisualComponent { ClientWidth: > 0, ClientHeight: > 0 } root
            ? new Rect(0, 0, root.ClientWidth, root.ClientHeight)
            : control.PreviousArrangeSlot ?? new Rect(control.DesiredSize);
        if (LayoutTrace.Enabled)
        {
            var name = string.IsNullOrEmpty(node.Name) ? node.GetType().Name : node.Name;
            LayoutTrace.Log($"ARRANGE-DIRTY {name}: -> Arrange({slot})");
        }
        control.Arrange(slot);
    }

    private static void MeasureControl(IMeasurableComponent control, Double width, Double height)
    {
        if (!Double.IsNaN(width) && !Double.IsNaN(height))
        {
            control.Measure(new Size(width, height));
        }
        else if (Double.IsNaN(width) && !Double.IsNaN(height))
        {
            control.Measure(new Size(Double.PositiveInfinity, height));
        }
        else if (!Double.IsNaN(width) && Double.IsNaN(height))
        {
            control.Measure(new Size(width, Double.PositiveInfinity));
        }
        else
        {
            control.Measure(Size.Infinity);
        }
    }

    /// <summary>
    /// A set of dirty nodes drained ancestors-first. Dedup via a membership set (a node enqueued twice produces one
    /// entry); ordered by visual depth via a min-heap so a parent is processed before its children and its
    /// measure/arrange cascade validates them (their own dequeue then no-ops on the validity gate). Depth is computed
    /// at enqueue time (it can change with reparenting); a slightly stale order only costs a little redundant work, not
    /// correctness, because the per-node validity gates make re-processing safe.
    /// </summary>
    private sealed class DirtyQueue
    {
        private readonly PriorityQueue<IUIComponent, int> _heap = new();
        private readonly HashSet<IUIComponent> _members = new();

        public bool IsEmpty => _members.Count == 0;

        public void Enqueue(IUIComponent node)
        {
            if (!_members.Add(node)) return;
            _heap.Enqueue(node, Depth(node));
        }

        /// <summary>Removes all currently-queued nodes into <paramref name="buffer"/> in ancestors-first (depth) order.
        /// Nodes enqueued AFTER this returns (re-dirtied during processing) stay queued for the next drain.</summary>
        public void DrainInto(List<IUIComponent> buffer)
        {
            buffer.Clear();
            while (_heap.Count > 0)
            {
                var node = _heap.Dequeue();
                if (_members.Remove(node)) buffer.Add(node);
            }
        }

        private static int Depth(IUIComponent node)
        {
            var depth = 0;
            var parent = node.VisualParent;
            while (parent != null)
            {
                depth++;
                parent = parent.VisualParent;
            }
            return depth;
        }
    }
}
