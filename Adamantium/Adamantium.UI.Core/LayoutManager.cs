using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Adamantium.Mathematics;
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

    private readonly IUIComponent _root;
    private readonly DirtyQueue _toStyle = new();
    private readonly DirtyQueue _toMeasure = new();
    private readonly DirtyQueue _toArrange = new();
    private readonly List<IUIComponent> _passBuffer = new();   // reused snapshot buffer for one phase's drain

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
        var top = node;
        while (top.VisualParent != null)
        {
            top = top.VisualParent;
        }
        return GetOrCreate(top);
    }

    public void InvalidateStyle(IUIComponent node) => _toStyle.Enqueue(node);

    public void InvalidateMeasure(IUIComponent node)
    {
        // A measure-invalid node also needs re-arranging; enqueue it for both so the arrange phase re-runs after the
        // measure phase recomputes sizes.
        _toMeasure.Enqueue(node);
        _toArrange.Enqueue(node);
    }

    public void InvalidateArrange(IUIComponent node) => _toArrange.Enqueue(node);

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
        // Forward-progress safety net: if the root itself is dirty but was never enqueued (e.g. it was invalidated
        // during construction, before this manager existed / before the subtree was assembled under it), seed it now.
        // This is O(1) - two flag reads on the root - not a tree walk, so a clean frame still costs nothing.
        if (_root is IMeasurableComponent rootMeasurable)
        {
            if (!rootMeasurable.IsMeasureValid) InvalidateMeasure(_root);
            else if (!rootMeasurable.IsArrangeValid) _toArrange.Enqueue(_root);
        }

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

            // Drain each queue as a SNAPSHOT (process only what's queued now). Work re-dirtied DURING a phase - e.g. an
            // arrange that re-invalidates a measure - lands back in the queues and is handled on the NEXT iteration. That
            // keeps the phases ordered (all measure before arrange) and lets a re-entrant invalidation converge, instead
            // of consuming an arrange entry before its re-measure has run (which would leave the node unarranged).
            _toStyle.DrainInto(_passBuffer);
            foreach (var node in _passBuffer) ApplyTheme(node);

            _toMeasure.DrainInto(_passBuffer);
            foreach (var node in _passBuffer) MeasureDirty(node);

            _toArrange.DrainInto(_passBuffer);
            foreach (var node in _passBuffer) ArrangeDirty(node);
        }

        if (didWork) LayoutUpdated?.Invoke(this, EventArgs.Empty);
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

        // Queued measure nodes are always the TOP-most measure-invalid node of their branch (see
        // MeasurableUIComponent.InvalidateMeasure), so measuring them with their own Width/Height (window: client size)
        // is correct - exactly as the old driver did. MeasureOverride then cascades down, the validity gate skipping
        // any clean subtree.
        if (LayoutTrace.Enabled)
        {
            var name = string.IsNullOrEmpty(node.Name) ? node.GetType().Name : node.Name;
            LayoutTrace.Log($"MEASURE-DIRTY {name}");
        }

        if (node is IWindow wnd)
        {
            MeasureControl(control, wnd.ClientWidth, wnd.ClientHeight);
        }
        else
        {
            MeasureControl(control, control.Width, control.Height);
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
        var slot = control.PreviousArrangeSlot ?? new Rect(control.DesiredSize);
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
