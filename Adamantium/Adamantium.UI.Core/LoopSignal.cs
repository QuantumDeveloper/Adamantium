using System;
using System.Threading;
using System.Threading.Channels;

namespace Adamantium.UI.Core;

/// <summary>
/// THE update loop's pipe - one channel, the same one the dispatcher posts to. Cross-thread work (window input and every other
/// event marshalled off the message-pump thread) is written here as an <see cref="Action"/>; everything else that merely makes
/// the UI owe another frame (a layout invalidation, a render-dirty mark, a queued binding update) writes a bare wake token.
/// The loop BLOCKS on reading this channel between frames, so an idle window - nobody moving the mouse, nothing animating,
/// minimized - costs exactly nothing, instead of a thread spinning through Update after Update to re-discover that nothing has
/// changed.
///
/// It has to be ONE channel: a separate "wake" primitive alongside the dispatcher queue can only ever be woken for one of the
/// two, and then the loop either sleeps through posted input or spins on the marks. Post and Request are the same pipe.
///
/// A wake token is DEDUPED (<see cref="_wakePending"/>): a frame that dirties ten thousand components must queue "there is
/// work", not ten thousand wake-ups. Posted actions are never deduped - each one is real work that has to run.
///
/// ANIMATIONS are the one thing this cannot express: they are TIME-driven, not event-driven - nothing "happens" to wake them,
/// they simply need the next frame - so while one runs the loop is scheduled by its frame pacing instead of by this channel.
/// </summary>
public static class LoopSignal
{
    // Single reader: the loop thread. Unbounded: a posted action must never be dropped.
    private static readonly Channel<Action> Pipe =
        Channel.CreateUnbounded<Action>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>The "there is work" token. Carries no action - reading it IS the wake.</summary>
    private static readonly Action WakeToken = static () => { };

    private static int _wakePending;   // 0/1 - at most one wake token is ever queued at a time

    /// <summary>Queues work to run on the loop thread (drained at the start of the next frame). Its arrival is itself a wake.</summary>
    public static void Post(Action action) => Pipe.Writer.TryWrite(action);

    /// <summary>Something changed - the loop owes another frame. Idempotent and near-free; safe from any thread.</summary>
    public static void Request()
    {
        if (Interlocked.Exchange(ref _wakePending, 1) == 0)
            Pipe.Writer.TryWrite(WakeToken);
    }

    /// <summary>Runs every posted action in order, on the loop thread, at the start of a frame. Clears the wake token FIRST, so
    /// anything signalled while this frame runs queues a fresh one and becomes the wake for the next frame - never swallowed.</summary>
    public static void Drain()
    {
        Interlocked.Exchange(ref _wakePending, 0);
        while (Pipe.Reader.TryRead(out var action))
        {
            if (ReferenceEquals(action, WakeToken)) continue;
            try { action(); }
            catch (Exception ex) { Console.WriteLine(ex); }
        }
    }

    /// <summary>Blocks the loop thread until something is in the pipe, or <paramref name="timeoutMs"/> elapses. The timeout is a
    /// safety net, not a schedule: every source that needs a frame writes here, so it only bounds the damage of one nobody
    /// thought of - a late frame rather than a frozen window.</summary>
    public static void Wait(int timeoutMs, CancellationToken token)
    {
        try
        {
            var pending = Pipe.Reader.WaitToReadAsync(token);
            if (pending.IsCompleted)
            {
                pending.GetAwaiter().GetResult();   // observe the result; the items stay in the pipe for Drain
                return;
            }
            // Only allocates when the loop actually goes to sleep - i.e. when the UI is idle, which is exactly when nobody cares.
            pending.AsTask().Wait(timeoutMs, token);
        }
        catch (OperationCanceledException) { /* shutting down */ }
        catch (AggregateException) { /* the wait was cancelled */ }
    }
}
