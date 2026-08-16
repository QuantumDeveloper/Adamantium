using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Adamantium.UI.Core.Diagnostics;

/// <summary>
/// Opt-in layout diagnostics, in two shapes. <see cref="Log"/> narrates individual measure/arrange calls - readable, but
/// only usable on a handful of them. <see cref="Count"/> aggregates instead: it answers "who marks layout dirty, and how
/// often" on a scene that produces tens of thousands of invalidations per second, where narrating would cost more than
/// the thing being measured. Both are OFF by default and cost one bool check.
/// </summary>
public static class LayoutTrace
{
    public static bool Enabled;
    public static Action<string> Sink;

    public static void Log(string message)
    {
        if (Enabled) Sink?.Invoke(message);
    }

    /// <summary>Turns the counters on. Kept separate from <see cref="Enabled"/>: the narrating trace and the counters are
    /// used on different scenes and never at the same time.</summary>
    public static bool Counting;

    // One shared map with interlocked increments. Layout runs on the UI thread, rebinds write from workers and the dump
    // reads from a third, so per-thread maps needed a merge - and a merge that quietly missed a thread looks exactly like
    // "this code never runs", which is the one conclusion a diagnostic must never invite.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type Owner, string Member), StrongBox<int>> _counts = new();

    /// <summary>Record one invalidation by WHO caused it: the component type and the property that changed, or a marker
    /// for a direct call. Keyed by Type + name, so nothing is allocated or formatted per event.</summary>
    public static void Count(Type owner, string member)
    {
        if (!Counting) return;

        var box = _counts.GetOrAdd((owner, member), _ => new StrongBox<int>());
        System.Threading.Interlocked.Increment(ref box.Value);
    }

    public static void ResetCounts() => _counts.Clear();

    /// <summary>Also record WHO called - by walking the stack. Far too slow for the running app, and exactly right for a
    /// headless probe: "this element is arranged four times per pass" is useless until the four callers are named.</summary>
    public static bool CountCallers;

    public static void CountCaller(Type owner, string member, int skipFrames)
    {
        if (!Counting || !CountCallers) return;

        var frame = new System.Diagnostics.StackTrace(skipFrames + 1, false).GetFrame(0);
        var method = frame?.GetMethod();
        var caller = method == null ? "?" : $"{method.DeclaringType?.Name}.{method.Name}";
        Count(owner, member + " <- " + caller);
    }

    /// <summary>How many invalidations are currently counted - so a caller can keep the BUSIEST window instead of
    /// whichever one happened to be current when it looked. A window that resets on a timer loses the very event it was
    /// set up to catch, because the interesting second is rarely the last one.</summary>
    public static int TotalCount()
    {
        var total = 0;
        foreach (var pair in _counts) total += pair.Value.Value;
        return total;
    }

    /// <summary>The counters, biggest first: "Type.Member  count".</summary>
    public static string DumpCounts()
    {
        var text = new StringBuilder();
        var total = 0;
        foreach (var pair in _counts.OrderByDescending(p => p.Value.Value))
        {
            total += pair.Value.Value;
            text.Append(pair.Value.Value.ToString().PadLeft(8)).Append("  ")
                .Append(pair.Key.Owner.Name).Append('.').Append(pair.Key.Member).Append('\n');
        }

        // A version marker, so the dump proves WHICH build produced it: a counter that records nothing otherwise reads as
        // "this code never runs", which is indistinguishable from "you are reading yesterday's binary".
        return $"counters v2  keys {_counts.Count}  counting {Counting}\ntotal {total}\n{text}";
    }
}
