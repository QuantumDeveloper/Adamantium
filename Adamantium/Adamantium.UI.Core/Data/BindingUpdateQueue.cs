using System.Collections.Generic;

namespace Adamantium.UI.Core.Data;

/// <summary>
/// F2 binding-storm batching. Instead of pushing a source change to its target synchronously (the WPF model, where
/// thousands of source changes per frame become thousands of inline target writes + layout invalidations on one stack),
/// a binding expression marks itself dirty here on a source change and is applied once per frame, COALESCED: N changes
/// to one binding collapse to a single apply of the final value.
/// </summary>
/// <remarks>
/// <see cref="Flush"/> runs once per frame, at the start of the layout pass and BEFORE the layout drain, so the target
/// writes (and the measure/arrange invalidations they trigger) land in THIS frame's layout queue. A dependent chain
/// (A→B, where applying A re-dirties B) is drained WITHIN the flush, so layout never runs on a half-propagated graph -
/// see <see cref="MaxCascadeRounds"/>. Enqueue is thread-safe (a source can change on a background thread); the apply
/// happens on the flushing (layout/UI) thread.
/// </remarks>
public static class BindingUpdateQueue
{
    private static readonly object Sync = new();
    private static readonly HashSet<BindingExpressionBase> Dirty = new();
    private static readonly List<BindingExpressionBase> Batch = new();   // reused snapshot buffer

    /// <summary>F2 budget: the maximum number of binding updates applied per <see cref="Flush"/> (i.e. per frame).
    /// Default 10000 - high enough that a normal frame's handful of updates never hit it, low enough to bound a binding
    /// storm (anything over the cap stays dirty and drains over later frames). Set to 0 to disable (apply all each
    /// frame), or raise/lower it (e.g. 50000) to taste.</summary>
    public static int MaxAppliesPerFlush { get; set; } = 10000;

    /// <summary>Marks an expression for the next coalesced flush (deduped: enqueuing twice still applies once).</summary>
    public static void Enqueue(BindingExpressionBase expression)
    {
        lock (Sync) Dirty.Add(expression);
        LoopSignal.Request();   // a value is waiting to be pushed to its target - the loop owes a frame (see LoopSignal)
    }

    /// <summary>Drops a (e.g. closed) expression so a dead binding is never applied.</summary>
    public static void Remove(BindingExpressionBase expression)
    {
        lock (Sync) Dirty.Remove(expression);
    }

    /// <summary>How many times a single <see cref="Flush"/> re-drains what its own applies dirtied. Deep enough for any
    /// real dependent chain, finite so a pair of bindings that never settle costs a few rounds a frame instead of hanging
    /// the loop - the leftovers drain over later frames, exactly as an over-budget batch does.</summary>
    private const int MaxCascadeRounds = 8;

    /// <summary>Applies every pending binding update (coalesced), including the ones those applies dirty in turn, so the
    /// graph is settled before layout reads it.</summary>
    public static void Flush()
    {
        // Draining the cascade here, rather than leaving it to the next frame, is what keeps a control whose geometry
        // depends on TWO bound properties from being laid out with one of them still stale. A RangeSlider bound to a
        // shared view-model got its new Maximum in this flush but the matching UpperValue - which another control's
        // coercion had just written back to that view-model - only in the next one, so every step of the drag arranged
        // one frame with the thumb short of the rail's end and the next frame back on it: a thumb visibly shivering at
        // the edge. The budget still applies ACROSS the rounds, so the storm cap is unchanged.
        var remaining = MaxAppliesPerFlush > 0 ? MaxAppliesPerFlush : int.MaxValue;
        for (var round = 0; round < MaxCascadeRounds && remaining > 0; round++)
        {
            var applied = FlushOnce(remaining);
            if (applied == 0) return;
            remaining -= applied;
        }
    }

    /// <summary>One coalesced pass over the currently-dirty expressions; returns how many were applied.</summary>
    private static int FlushOnce(int budget)
    {
        lock (Sync)
        {
            if (Dirty.Count == 0) return 0;
            Batch.Clear();
            if (Dirty.Count > budget)
            {
                // Over budget: apply only the first N this flush; the rest stay dirty and drain over later frames.
                foreach (var expression in Dirty)
                {
                    Batch.Add(expression);
                    if (Batch.Count >= budget) break;
                }
                foreach (var expression in Batch)
                    Dirty.Remove(expression);
            }
            else
            {
                Batch.AddRange(Dirty);
                Dirty.Clear();
            }
        }
        // Apply OUTSIDE the lock: ApplyPending runs converters + SetValue and can re-enter Enqueue (dependent bindings),
        // which would deadlock under the lock; those re-enqueues land in Dirty for the next round.
        var count = Batch.Count;
        foreach (var expression in Batch)
            expression.ApplyPending();   // diagnostics counted in UpdateTarget (the actual target write), not here
        Batch.Clear();
        return count;
    }
}
