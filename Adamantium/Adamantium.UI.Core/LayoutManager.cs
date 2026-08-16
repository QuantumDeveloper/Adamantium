using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Diagnostics;

namespace Adamantium.UI.Core;

/// <summary>
/// Owns the per-frame layout pass for one visual-tree root (a window / top-level): the single driver of style-application
/// + measure + arrange. Invalidation registers the affected node in this manager's dirty queues;
/// <see cref="ExecuteLayoutPass"/> drains only those, so a clean frame (nothing invalid) walks nothing. One manager per
/// root, kept persistently so invalidations BETWEEN passes accumulate; a node finds its manager by its top-most visual
/// ancestor (<see cref="For"/>). See docs/LAYOUT_MANAGER_PLAN.md.
/// </summary>
public sealed class LayoutManager
{
    // Persistent per-root managers, keyed (weakly) by the top-most visual node so they are GC'd with their tree.
    private static readonly ConditionalWeakTable<IUIComponent, LayoutManager> Managers = new();

    // Backstop against a node that re-dirties itself every time it is laid out: bail the drain loop after this many iterations.
    private const int MaxPassIterations = 100;

    // No per-frame TIME budget: a pass always drains FULLY, so the drawn frame is internally consistent. An earlier budget
    // that cut a pass mid-way and re-queued the tail published TORN frames (a grid with tiles of two sizes). What replaced it:
    // the compositor presents at its own pace, and heavy INTAKE is bounded at the source (a virtualizing panel realizes only
    // viewport+margin, slicing big realizes over frames via InvalidateMeasureNextPass). See docs/TECH_DEBT.md.

    private readonly IUIComponent _root;
    private readonly DirtyQueue _toStyle = new();
    private readonly DirtyQueue _toMeasure = new();
    private readonly DirtyQueue _toArrange = new();
    // Nodes asking to be re-measured NEXT pass, not this one (a virtualizing panel slicing a large realize over frames).
    // Enqueuing into _toMeasure would drain it THIS pass - the very burst we spread. Promoted at the start of each pass.
    private readonly HashSet<IUIComponent> _toMeasureNextPass = new();
    private readonly List<IUIComponent> _passBuffer = new();   // reused snapshot buffer for one phase's drain
    private readonly List<IUIComponent> _promoteBuffer = new();   // reused scratch for promoting next-pass deferrals
    private readonly System.Diagnostics.Stopwatch _passStopwatch = new();   // reused per pass (RuntimeStats)

    public LayoutManager(IUIComponent root)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
    }

    /// <summary>Gets (creating once) the manager that owns the given root (a window at runtime, a subtree root in tests).</summary>
    public static LayoutManager GetOrCreate(IUIComponent root) => Managers.GetValue(root, static r => new LayoutManager(r));

    /// <summary>Resolves the manager responsible for <paramref name="node"/> via its top-most visual ancestor.</summary>
    public static LayoutManager For(IUIComponent node)
    {
        // RootVisual is cached + kept current by the attach/detach walk, so read it directly (O(1)) - For() is on the
        // invalidation hot path. A not-yet-attached / plain test tree has RootVisual == null; fall back to the walk to its
        // local top (the same key the pass is driven from, so the resolved manager is identical either way).
        // Overlay content is DRAWN by the window but laid out by the popup layer, which alone knows its constraint and
        // its slot. Its invalidations must therefore resolve here, to a manager of its own - joining the window's queue
        // put two owners on one virtualizing panel and re-entered its generator mid-enumeration.
        if (node.LayoutRoot is { } owner)
        {
            return GetOrCreate(owner);
        }

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

    // Every invalidation owes the loop another pass, so wake it: layout is NOT covered by render-dirty marks, so without
    // this the loop only woke on its 250 ms safety timeout and a tab's content crawled in at ~4 passes/sec. See LoopSignal.
    public void InvalidateStyle(IUIComponent node)
    {
        _toStyle.Enqueue(node);
        LoopSignal.Request();
    }

    public void InvalidateMeasure(IUIComponent node)
    {
        LoopSignal.Request();
        // Parallel-rebind window: a rebind's synchronous writes flip AffectsMeasure -> InvalidateMeasure off worker threads,
        // which would concurrently mutate this root's (non-thread-safe) DirtyQueue. Collect lock-free, replay when the pass
        // ends (see BeginDeferredInvalidation).
        if (_deferInvalidations) { DeferredMeasure.Enqueue(node); return; }
        // A measure-invalid node also needs re-arranging: enqueue both so arrange re-runs after measure recomputes sizes.
        _toMeasure.Enqueue(node);
        _toArrange.Enqueue(node);
    }

    public void InvalidateArrange(IUIComponent node)
    {
        LoopSignal.Request();
        if (_deferInvalidations) { DeferredArrange.Enqueue(node); return; }
        _toArrange.Enqueue(node);
    }

    // ---- Parallel-rebind deferred invalidation ----
    // While a virtualizing panel rebinds+measures its (disjoint) tiles across cores, the only shared escape is the per-root
    // DirtyQueue enqueue above; route those into these lock-free queues instead and replay on the coordinating thread once
    // the parallel pass joins. Static: the flag toggles around a Parallel.ForEach (fork/join barrier), so one switch suffices.
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

    /// <summary>Requests <paramref name="node"/> be re-measured on the NEXT pass, not this one - a virtualizing panel
    /// continuing a sliced realize. Safe mid-pass: it doesn't touch this pass's queues.</summary>
    // Nothing else will wake the loop for this (the work is already known), so signal here or the fill stalls until the timeout.
    public void InvalidateMeasureNextPass(IUIComponent node)
    {
        _toMeasureNextPass.Add(node);
        LoopSignal.Request();
    }

    /// <summary>Raised at the end of a pass that actually did work (queues drained) - layout settled for this frame. Not
    /// raised on a clean frame, so a consumer (e.g. the render cache) can rebuild on this instead of every frame.</summary>
    public event EventHandler LayoutUpdated;

    /// <summary>Raised after a pass that found NO work: every queue empty AND nothing re-dirtied, so this tree has SETTLED.</summary>
    /// <remarks>
    /// Distinct from <see cref="LayoutUpdated"/> (which fires after a pass that DID work - mid-cascade, just one of several).
    /// A theme swap drains over several passes that each look "settled" in the LayoutUpdated sense; only a workless pass
    /// proves nothing is left. Static because a settle concerns whoever started the cascade, not one root (see <see cref="IsSettled"/>).
    /// </remarks>
    public static event Action<LayoutManager> Quiescent;

    /// <summary>True when this root owes no layout work at all (nothing queued, nothing deferred to the next pass).</summary>
    public bool IsSettled => _toStyle.IsEmpty && _toMeasure.IsEmpty && _toArrange.IsEmpty && _toMeasureNextPass.Count == 0;


    /// <summary>
    /// Runs one layout pass: drain style (themes can change templates, so it precedes measure), then measure, then arrange,
    /// ancestors-first within each. Re-dirtying during the pass loops until all queues drain.
    /// </summary>
    public void ExecuteLayoutPass()
    {
        // Apply this frame's batched (coalesced) binding updates BEFORE laying out, so their target writes and the
        // invalidations they trigger drain in this same pass. The global queue flushes once/frame (first root empties it).
        BindingUpdateQueue.Flush();

        // Promote nodes that deferred to this pass. Snapshot+clear first: a promoted node may re-defer for the NEXT pass,
        // which must land in the now-empty set. InvalidateMeasure (not a bare enqueue) so the validity flag is cleared.
        if (_toMeasureNextPass.Count > 0)
        {
            _promoteBuffer.Clear();
            foreach (var node in _toMeasureNextPass) _promoteBuffer.Add(node);   // struct enumerator, no alloc
            _toMeasureNextPass.Clear();
            foreach (var node in _promoteBuffer)
                if (node is IMeasurableComponent measurable) measurable.InvalidateMeasure();
        }

        // Forward-progress safety net: if the root is dirty but was never enqueued (invalidated during construction, before
        // this manager existed), seed it now. O(1) - two flag reads - so a clean frame still costs nothing.
        if (_root is IMeasurableComponent rootMeasurable)
        {
            if (!rootMeasurable.IsMeasureValid) InvalidateMeasure(_root);
            else if (!rootMeasurable.IsArrangeValid) _toArrange.Enqueue(_root);
        }

        _passStopwatch.Restart();   // time the pass for RuntimeStats

        var didWork = false;
        var iterations = 0;
        while (!_toStyle.IsEmpty || !_toMeasure.IsEmpty || !_toArrange.IsEmpty)
        {
            if (++iterations > MaxPassIterations)
            {
                if (LayoutTrace.Enabled) LayoutTrace.Log($"LAYOUT PASS aborted after {MaxPassIterations} iterations (re-dirty loop)");
                break;
            }
            didWork = true;

            // Drain each queue as a SNAPSHOT (only what's queued now), ordered style -> measure -> arrange. Work re-dirtied
            // DURING a phase lands back in the queues and is handled on the NEXT iteration, letting re-entrancy converge.
            DrainPhase(_toStyle, ApplyTheme);
            DrainPhase(_toMeasure, MeasureDirty);
            DrainPhase(_toArrange, ArrangeDirty);
        }

        var settled = _toStyle.IsEmpty && _toMeasure.IsEmpty && _toArrange.IsEmpty;

        // A pass that found NOTHING to do is the "swap has settled" signal for RenderDirty.ForceStructuralUntilSettled
        // (theme/DPI): every settle write flows through this pass, so a workless pass means the cascade is done.
        if (!didWork)
        {
            RenderDirty.NotifyLayoutQuiescent();
            Quiescent?.Invoke(this);
        }

        RuntimeStats.LastLayoutPassMs = _passStopwatch.Elapsed.TotalMilliseconds;
        if (didWork) Diagnostics.LayoutTrace.Count(typeof(LayoutManager), "*pass*");
        RuntimeStats.LastPassBudgetDeferred = !settled;

        // LayoutUpdated = "layout settled this frame" - only when everything drained.
        if (didWork && settled)
            LayoutUpdated?.Invoke(this, EventArgs.Empty);

        // Coalesced resource-change notification: the style drain may have loaded a new theme's dictionaries, so fire
        // ResourcesChanged once, here, after they're present. No-op (a flag read) when nothing changed.
        UIAppContext.Current?.ResourceManager?.FlushResourceChanges();
    }

    // Drains one queue FULLY as a snapshot (work re-dirtied during the phase waits for the next iteration), ancestors-first.
    private void DrainPhase(DirtyQueue queue, Action<IUIComponent> process)
    {
        queue.DrainInto(_passBuffer);
        for (var i = 0; i < _passBuffer.Count; i++)
            process(_passBuffer[i]);
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

        // Re-measure with the element's OWN cached constraint, NOT a guess; MeasureOverride cascades down, the validity gate
        // skipping any clean subtree. A root visual uses its client size; a never-measured top uses its Width/Height.
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

        // Propagate up only if the child's OUTWARD size changed (else the re-measure stayed contained). EXCEPT a parent that
        // is a MEASURE BOUNDARY (fixed size, or a virtualizing host whose extent is count×cell): propagating in is spurious
        // and, running outside the panel's _inLayout mute, re-dirties it every iteration and spins to MaxPassIterations
        // (draining the whole realize backlog in one pass). Honor the boundary so InvalidateMeasureNextPass is respected.
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

        if (node.Visibility == Visibility.Collapsed) return;

        if (!control.IsMeasureValid)
        {
            // Its measure was re-dirtied after this arrange was queued; defer to a later iteration (re-enqueue, don't drop)
            // - but ONLY while the node is still ours. A node that LEFT this tree (a rebuilt template, a closed popup, a
            // recycled container) keeps its stale arrange entry here while its measure invalidation goes to whatever root
            // owns it now, so this manager can never make it measure-valid: re-queueing it spins the pass to its iteration
            // cap every frame and the root NEVER settles. Whoever re-attaches it re-registers it (see
            // MeasurableUIComponent.OnAttachedToVisualTree), so dropping it here loses nothing.
            if (ReferenceEquals(For(node), this)) _toArrange.Enqueue(node);
            return;
        }

        // Arrange into the node's OWN last correct slot (preserved across invalidation), NOT parent.DesiredSize (the old
        // fallback piled dirty children at the parent origin). A never-arranged node (the root, first layout) fills its
        // measured area. The root visual is special: its slot is the LIVE client rect, never a saved slot - on a resize the
        // client grows and the root re-measures to it, but PreviousArrangeSlot still holds the OLD rect, which would pin the
        // window's clip (and root hit-testing) to the old size. Feed the live client rect so measure/arrange agree.
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
    /// A set of dirty nodes drained ancestors-first: dedup via a membership set, ordered by visual depth via a min-heap so a
    /// parent is processed before its children (their cascade validates them, so their own dequeue no-ops on the gate).
    /// Depth is computed at enqueue time; a stale order only costs redundant work, not correctness (the validity gates make
    /// re-processing safe).
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
