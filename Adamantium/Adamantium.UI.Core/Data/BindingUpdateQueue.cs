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
/// writes (and the measure/arrange invalidations they trigger) land in THIS frame's layout queue. The flush is a
/// snapshot: an expression re-dirtied during the flush (a dependent binding A→B) is applied on the NEXT frame, which
/// bounds per-frame work and lets dependent chains converge over a few frames. Enqueue is thread-safe (a source can
/// change on a background thread); the apply happens on the flushing (layout/UI) thread.
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
    }

    /// <summary>Drops a (e.g. closed) expression so a dead binding is never applied.</summary>
    public static void Remove(BindingExpressionBase expression)
    {
        lock (Sync) Dirty.Remove(expression);
    }

    /// <summary>Applies every pending binding update once (coalesced). Called once per frame before layout.</summary>
    public static void Flush()
    {
        lock (Sync)
        {
            if (Dirty.Count == 0) return;
            Batch.Clear();
            if (MaxAppliesPerFlush > 0 && Dirty.Count > MaxAppliesPerFlush)
            {
                // Over budget: apply only the first N this flush; the rest stay dirty and drain over later frames.
                foreach (var expression in Dirty)
                {
                    Batch.Add(expression);
                    if (Batch.Count >= MaxAppliesPerFlush) break;
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
        // which would deadlock under the lock; those re-enqueues land in Dirty for the next flush.
        foreach (var expression in Batch)
            expression.ApplyPending();
        Batch.Clear();
    }
}
