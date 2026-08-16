using System;
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

    /// <summary>TEMP: the unit type that last cost a frame its patch.</summary>
    public static string Refuser;

    public static void Add(double drawMs, byte kind, bool replayed, int clones, byte why, int cache, int composited,
        int ops, int unitOps)
    {
        if (!Enabled) return;

        // A ring is the wrong shape for a RARE event: at 450 fps it holds a few seconds, so a hand-made incident (hover,
        // a dropdown) is pushed out while the tester is still typing "done". Frames that ran LONG are kept separately and
        // never evicted. Filtered by COST, not by path: an empty adorner layer walks every single frame at 0.03 ms and
        // filled the whole list with itself in seconds, evicting the room for what was being hunted.
        if (drawMs > 5.0)
        {
            if (_incidents.Count < IncidentLimit)
            {
                _incidents.Add(new Entry
                {
                    DrawMs = drawMs, Kind = kind, Replayed = replayed, Clones = clones, Why = why,
                    By = why is 5 or 6 ? Refuser : null, Cache = cache, Composited = composited, Ops = ops, UnitOps = unitOps,
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
        _ring[i].By = why is 5 or 6 ? Refuser : null;
        _ring[i].Cache = cache;
        _ring[i].Composited = composited;
        _ring[i].Ops = ops;
        _ring[i].UnitOps = unitOps;
    }

    private const int IncidentLimit = 4096;

    private static readonly System.Collections.Generic.List<Entry> _incidents = new();

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
