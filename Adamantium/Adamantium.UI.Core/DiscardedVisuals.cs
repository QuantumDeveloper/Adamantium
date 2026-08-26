using System;
using System.Collections.Generic;

namespace Adamantium.UI.Core;

/// <summary>
/// Announces that visuals have been DESTROYED, and releases them - later, a bounded number at a time, in the idle
/// space between frames. The counterpart to <see cref="ParkedVisuals"/>: an element that left the tree either went on
/// purpose and is coming back, or it is gone for good, and only whoever tore it down can tell the two apart. What it
/// says is recorded on the element as its <see cref="VisualLifecycle"/>; this is where the consequences are paid.
/// </summary>
/// <remarks>
/// WHY this exists. A component is held by two kinds of thing, and both used to have to notice a departure by
/// themselves: what the element SUBSCRIBED to (a view model's collection, a theme resource), undone only when the
/// source is replaced - and nothing replaces the source of something thrown away; and what a SUBSYSTEM keyed by the
/// element (the render cache's maps, a brush's owner list, the layout queues). The ones that forgot leaked: measured on
/// a theme swap, well over a thousand elements a swap retained for the life of the application.
/// <para>DEFERRED, and that is not a detail. Releasing a subtree is a walk per element - bindings, behaviours, and then
/// every subsystem sweeping its own structures - and doing it inside the frame that swaps the content made switching to
/// a heavy tab stall for seconds. Nothing here runs during a teardown; a teardown only says what died, in O(1) per
/// element, and the work happens when the loop has nothing better to do.</para>
/// <para>The delay also buys CORRECTNESS, which is the part worth keeping. Between being queued and being reached, an
/// element can come back - a keep-alive view returning, a pooled container taking a new item - and the drain re-reads
/// its state and skips it. Releasing immediately meant deciding at the one moment when the answer is least certain:
/// mid-rebuild, with the element still taking part in what replaces it.</para>
/// <para>BATCHED. A theme swap discards well over a thousand parts, and a subsystem sweeping its own map does one pass
/// either way - a thousand point lookups would be strictly worse. Subscribers get whole batches.</para>
/// </remarks>
public static class DiscardedVisuals
{
    /// <summary>A handler for <see cref="Discarded"/>. Its own delegate type because the batch is a
    /// <see cref="ReadOnlySpan{T}"/>, and a ref struct cannot be a generic argument - there is no
    /// <c>Action&lt;ReadOnlySpan&lt;T&gt;&gt;</c>.</summary>
    public delegate void DiscardedHandler(ReadOnlySpan<IFundamentalUIComponent> gone);

    /// <summary>Raised from the DRAIN, once per batch actually released - never during a teardown. Handlers must not
    /// throw: a subsystem failing to let go must not stop the others.
    /// <para>The batch is a SPAN, deliberately: a ref struct cannot be stored in a field, captured by a lambda or
    /// carried into an async method, so a handler physically cannot keep the departure around. That is the discipline
    /// this class exists to enforce - a subsystem that stashed the list would be re-creating the very bug it subscribed
    /// here to fix. It costs the caller nothing: a List is passed with CollectionsMarshal.AsSpan, no copy.</para></summary>
    public static event DiscardedHandler Discarded;

    // Waiting to be released. Elements only - what holds them is asked at drain time, not now.
    private static readonly Queue<FundamentalUIComponent> Pending = new();

    // Reused across drains: the batch handed to subscribers.
    private static readonly List<IFundamentalUIComponent> Batch = new();

    /// <summary>How many are still waiting. Zero means the last teardown has been fully paid for.</summary>
    public static int PendingCount { get { lock (Pending) return Pending.Count; } }

    /// <summary>Called by <see cref="FundamentalUIComponent.MarkDiscarded"/> - the O(1) half of a teardown.</summary>
    internal static void Enqueue(FundamentalUIComponent gone)
    {
        if (gone == null) return;
        lock (Pending) Pending.Enqueue(gone);
    }

    /// <summary>Called by a teardown that has destroyed visuals: a control template being replaced, content being
    /// released. Records the state on each one and queues it; nothing is released here.</summary>
    public static void Publish(ReadOnlySpan<IFundamentalUIComponent> gone)
    {
        foreach (var component in gone)
        {
            (component as FundamentalUIComponent)?.MarkDiscarded();
        }
    }

    /// <summary>The single-element case. The span is over the parameter itself - a local - so this allocates nothing
    /// and needs no shared buffer to go wrong under re-entrancy.</summary>
    public static void Publish(IFundamentalUIComponent gone)
    {
        if (gone == null) return;

        Publish(System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref gone, 1));
    }

    /// <summary>Release up to <paramref name="budget"/> queued elements. Called from the loop's idle time, so the cost
    /// of a teardown lands where there is room for it rather than inside the frame that caused it. Returns how many
    /// were actually released - callers that want the queue empty can drain until this returns zero.
    /// <para>Anything that has come back since being queued is dropped without being touched: the state is re-read
    /// HERE, which is the whole reason for waiting.</para></summary>
    public static int Drain(int budget)
    {
        if (budget <= 0) return 0;

        Batch.Clear();
        lock (Pending)
        {
            while (Batch.Count < budget && Pending.Count > 0)
            {
                var candidate = Pending.Dequeue();
                if (candidate.Lifecycle == VisualLifecycle.Discarded) Batch.Add(candidate);
            }
        }

        if (Batch.Count == 0) return 0;

        // The element's own release first (its bindings and behaviours), then the subsystems keyed by it. In that
        // order because a subsystem's sweep may read the element, and an element that has let go of its bindings is
        // still a valid thing to read - whereas the reverse would have subsystems answering about an element whose
        // sources are still live.
        foreach (var component in Batch)
        {
            ((FundamentalUIComponent)component).ReleaseFromQueue();
        }

        Discarded?.Invoke(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(Batch));

        var released = Batch.Count;
        Batch.Clear();
        return released;
    }
}
