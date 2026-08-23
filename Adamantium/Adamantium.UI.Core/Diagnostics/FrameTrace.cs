using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.UI.Core.Diagnostics;

/// <summary>TEMPORARY: a per-FRAME trace of how the draw pass spent itself, kept in a ring in MEMORY. The overlay dumps
/// it once a second - the point is that recording costs one array write, so the trace cannot become the thing it
/// measures (a per-frame file append already did that once). Remove with the experiment.</summary>
public static class FrameTrace
{
    public struct Entry
    {
        public double DrawMs;
        public byte Kind;       // RenderBuildKind
        public bool Replayed;
        public int Clones;
        public byte Why;
        public string By;
        public int Cache;       // which RenderCache drew it - several (windows, adorner layers) share this ring
        public double LayoutMs; // the LAYOUT pass and the RECORD pass of the same frame - so a spike can be attributed
        public double BuildMs;
        public int Composited;  // compositor entries applied on this frame (an animation the render thread plays itself)
        public int Ops;         // recorded draw operations replayed
        public int UnitOps;     // ...of which per-unit draws (each its own pipeline + uniforms)
    }

    // Several caches share the ring, so 512 entries was under a second of real time - short enough that a hand-made event
    // (moving a highlight, then letting go) was pushed out before the dump. Sized to hold tens of seconds instead.
    private const int Capacity = 8192;

    private static readonly Entry[] _ring = new Entry[Capacity];
    private static int _next;

    public static bool Enabled;

    /// <summary>TEMP: the unit type that last cost a frame its patch. Every assignment is TALLIED below, because the
    /// name of one frame's refuser answers "what happened here" and not "what is worth fixing" - there are ten exits and
    /// only a count over a run says which of them a scenario actually takes.</summary>
    public static string Refuser
    {
        get => _refuser;
        set
        {
            _refuser = value;
            if (!Enabled || value == null) return;
            // Tally the REASON, not the instance: the detail carries a control's type and the words it draws, so keyed
            // whole it would be a list of thousands of one-offs instead of ten numbers.
            var cut = value.AsSpan().IndexOfAny('<', ' ');
            var reason = cut < 0 ? value : value[..cut];
            lock (Refusals) Refusals[reason] = Refusals.TryGetValue(reason, out var had) ? had + 1 : 1;
        }
    }

    private static string _refuser;

    /// <summary>TEMP: how many frames each refusal reason cost, over the whole run.</summary>
    public static readonly Dictionary<string, int> Refusals = new();

    /// <summary>TEMP: what the PATCH still re-bakes, counted by unit type over the whole run. "The patch is slow" is not
    /// a finding - which family it is still walking is, because each one is unblocked by different means.</summary>
    public static readonly Dictionary<string, int> Patched = new();

    /// <summary>TEMP: full walks so far this run - read once a second to tell a drop caused by walking the scene from one
    /// caused by anything else.</summary>
    public static int Walks;

    /// <summary>TEMP: how big a MOVE the patch tried to carry - components collected and instance-holding units re-baked,
    /// worst frame of the run, and how much of that work a refusal then threw away. A move is meant to be O(moved); these
    /// say whether some scenario makes it O(scene) and then walks anyway - paying twice.</summary>
    public static int MovedCollectedMax, MovedRebakedMax, MovedWastedMax, MovedRefusals;

    /// <summary>TEMP: why a frame's moves could NOT be carried, so why=3 stops saying only "something moved". This is the
    /// number that named the tab-switch drop: 261 frames of a 40 s run refused over one clip inside a sliding view.</summary>
    public static readonly Dictionary<string, int> NotCarried = new();

    public static void NoteNotCarried(string reason)
    {
        if (!Enabled) return;
        lock (NotCarried) NotCarried[reason] = NotCarried.TryGetValue(reason, out var had) ? had + 1 : 1;
    }

    public static void NoteMoved(int collected, int rebaked, bool refused)
    {
        if (!Enabled) return;
        if (collected > MovedCollectedMax) MovedCollectedMax = collected;
        if (rebaked > MovedRebakedMax) MovedRebakedMax = rebaked;
        if (!refused) return;
        MovedRefusals++;
        if (rebaked > MovedWastedMax) MovedWastedMax = rebaked;
    }

    public static void NotePatched(string what)
    {
        if (!Enabled) return;
        lock (Patched) Patched[what] = Patched.TryGetValue(what, out var had) ? had + 1 : 1;
    }

    /// <summary>TEMP: why the RECORD fell back to a full walk of the tree. A full walk is the most expensive frame there
    /// is, and "structural" is not a reason - the splice refuses for half a dozen unrelated causes and each has its own
    /// fix. Set at every point that gives up; read on the next frame that is recorded Full.</summary>
    public static string FullWalkReason;

    /// <summary>TEMP: WHAT made the retained stream stale against the layout (why=4). "The layout changed" is not a
    /// cause - a forgiven node move, a resize and a re-parent all land here and none of them is fixed the same way.</summary>
    public static string LayoutChangedBy;

    /// <summary>TEMP: which content took the slot-write fast path away from a motion node, counted by "node &lt;- content".</summary>
    public static readonly Dictionary<string, int> NotAware = new();

    public static void NoteNotAware(string what)
    {
        if (!Enabled) return;
        lock (NotAware) NotAware[what] = NotAware.TryGetValue(what, out var had) ? had + 1 : 1;
    }

    public static void Add(double drawMs, byte kind, bool replayed, int clones, byte why, int cache, int composited,
        int ops, int unitOps)
    {
        if (!Enabled) return;

        if (!replayed) Walks++;   // TEMP: counted per frame, read once a second by the sandbox timeline

        // A ring is the wrong shape for a RARE event: at 450 fps it holds a few seconds, so a hand-made incident (hover,
        // a dropdown) is pushed out while the tester is still typing "done". Frames that ran LONG are kept separately and
        // never evicted. Filtered by COST, not by path: an empty adorner layer walks every single frame at 0.03 ms and
        // filled the whole list with itself in seconds, evicting the room for what was being hunted.
        if (drawMs > 3.0)
        {
            if (_incidents.Count < IncidentLimit)
            {
                _incidents.Add(new Entry
                {
                    DrawMs = drawMs, Kind = kind, Replayed = replayed, Clones = clones, Why = why,
                    By = kind == 3 ? FullWalkReason : why == 4 ? LayoutChangedBy : why is 5 or 6 ? Refuser : null, Cache = cache, Composited = composited, Ops = ops, UnitOps = unitOps,
                    LayoutMs = RuntimeStats.LastLayoutPassMs, BuildMs = RuntimeStats.LastRenderBuildMs
                });
            }
        }

        var i = _next++ & (Capacity - 1);
        _ring[i].DrawMs = drawMs;
        _ring[i].Kind = kind;
        _ring[i].Replayed = replayed;
        _ring[i].Clones = clones;
        _ring[i].Why = why;
        _ring[i].By = kind == 3 ? FullWalkReason : why == 4 ? LayoutChangedBy : why is 5 or 6 ? Refuser : null;
        _ring[i].Cache = cache;
        _ring[i].Composited = composited;
        _ring[i].Ops = ops;
        _ring[i].UnitOps = unitOps;
    }

    private const int IncidentLimit = 4096;

    private static readonly System.Collections.Generic.List<Entry> _incidents = new();

    /// <summary>How many long frames have been recorded - a mark a caller can take now and read back from later, so a
    /// report can say "the ones from THIS second" instead of "all of them".</summary>
    public static int IncidentCount => _incidents.Count;

    /// <summary>The long frames recorded since <paramref name="mark"/>.</summary>
    public static string DumpIncidentsSince(int mark)
    {
        var text = new StringBuilder();
        for (var n = Math.Max(0, mark); n < _incidents.Count; n++) Format(text, _incidents[n]);
        return text.ToString();
    }

    /// <summary>Every frame that walked or ran long, oldest-first - kept out of the ring so it cannot be evicted.</summary>
    public static string DumpIncidents()
    {
        var text = new StringBuilder();
        for (var n = 0; n < _incidents.Count; n++)
        {
            Format(text, _incidents[n]);
        }

        return text.ToString();
    }

    /// <summary>The ring oldest-first, one line per frame.</summary>
    /// <summary>What a frame COSTS, said as a distribution rather than an average: an average hides exactly the thing a
    /// smooth window depends on, which is that the slow frames are rare AND not much slower. Walks and replays are
    /// reported apart, because mixing them averages two different frames into a number that describes neither.</summary>
    public static string Percentiles()
    {
        var count = Math.Min(_next, Capacity);
        if (count == 0) return "frame times: nothing recorded";

        var start = _next - count;
        // PER CACHE: a window and an empty adorner layer share this ring, and one costs milliseconds where the other
        // costs microseconds - mixed together they make a distribution that describes neither.
        var byCache = new Dictionary<(int Cache, bool Replayed), List<Entry>>();
        for (var n = 0; n < count; n++)
        {
            var e = _ring[(start + n) & (Capacity - 1)];
            var key = (e.Cache, e.Replayed);
            if (!byCache.TryGetValue(key, out var list)) byCache[key] = list = new List<Entry>();
            list.Add(e);
        }

        var text = new StringBuilder("frame draw ms, per cache:");
        foreach (var (key, entries) in byCache)
        {
            text.Append(Environment.NewLine).Append("  ").Append(Describe($"cache {key.Cache} {(key.Replayed ? "replay" : "walk")}", entries));
        }

        // WHY a frame walked instead of patching. A walk of a heavy scene costs tens of milliseconds where a patch costs
        // a fraction of one, so the reason is the whole question - and an average frame time cannot name it.
        var reasons = new Dictionary<byte, int>();
        for (var n = 0; n < count; n++)
        {
            var e = _ring[(start + n) & (Capacity - 1)];
            if (e.Replayed) continue;
            reasons[e.Why] = reasons.TryGetValue(e.Why, out var had) ? had + 1 : 1;
        }

        if (reasons.Count > 0)
        {
            text.Append(Environment.NewLine).Append("  walked because:");
            foreach (var (why, n) in reasons) text.Append(' ').Append(WhyName(why)).Append('=').Append(n);
        }

        return text.ToString();
    }

    private static string WhyName(byte why) => why switch
    {
        0 => "nothing-to-replay",
        1 => "nothing-recorded",
        2 => "stream-unusable",
        3 => "transform-dirty",
        4 => "layout-changed-since-record",
        5 => "splice-refused",
        6 => "slot-patch-refused",
        _ => "why-" + why
    };

    private static string Describe(string name, List<Entry> entries)
    {
        if (entries.Count == 0) return $"{name} none";

        entries.Sort(static (a, b) => a.DrawMs.CompareTo(b.DrawMs));
        double At(double q) => entries[Math.Min(entries.Count - 1, (int)(entries.Count * q))].DrawMs;

        double ops = 0, unitOps = 0;
        foreach (var e in entries) { ops += e.Ops; unitOps += e.UnitOps; }
        return $"{name} n={entries.Count} p50 {At(0.50):F2} p95 {At(0.95):F2} p99 {At(0.99):F2} max {entries[^1].DrawMs:F2}"
             + $" | ops {ops / entries.Count:F0} of which per-unit {unitOps / entries.Count:F0}";
    }

    public static string Dump()
    {
        var text = new StringBuilder();
        var count = Math.Min(_next, Capacity);
        var start = _next - count;

        for (var n = 0; n < count; n++)
        {
            Format(text, _ring[(start + n) & (Capacity - 1)]);
        }

        return text.ToString();
    }

    private static void Format(StringBuilder text, Entry e)
    {
        text.Append(e.DrawMs.ToString("F2")).Append(' ')
            .Append(e.Kind).Append(e.Replayed ? " replay" : " walk  ")
            .Append(" clones=").Append(e.Clones).Append(" why=").Append(e.Why)
            .Append(" by=").Append(e.By ?? "-")
            .Append(" cache=").Append(e.Cache).Append(" comp=").Append(e.Composited)
            .Append(" ops=").Append(e.Ops).Append(" unitOps=").Append(e.UnitOps)
            .Append(" layout=").Append(e.LayoutMs.ToString("F2"))
            .Append(" build=").Append(e.BuildMs.ToString("F2")).Append('\n');
    }
}
