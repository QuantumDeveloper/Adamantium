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
    }

    private const int Capacity = 512;

    private static readonly Entry[] _ring = new Entry[Capacity];
    private static int _next;

    public static bool Enabled;

    /// <summary>TEMP: the unit type that last cost a frame its patch.</summary>
    public static string Refuser;

    public static void Add(double drawMs, byte kind, bool replayed, int clones, byte why = 0)
    {
        if (!Enabled) return;

        var i = _next++ & (Capacity - 1);
        _ring[i].DrawMs = drawMs;
        _ring[i].Kind = kind;
        _ring[i].Replayed = replayed;
        _ring[i].Clones = clones;
        _ring[i].Why = why;
        _ring[i].By = why == 6 ? Refuser : null;
    }

    /// <summary>The ring oldest-first, one line per frame.</summary>
    public static string Dump()
    {
        var text = new StringBuilder();
        var count = Math.Min(_next, Capacity);
        var start = _next - count;

        for (var n = 0; n < count; n++)
        {
            ref var e = ref _ring[(start + n) & (Capacity - 1)];
            text.Append(e.DrawMs.ToString("F2")).Append(' ')
                .Append(e.Kind).Append(e.Replayed ? " replay" : " walk  ")
                .Append(" clones=").Append(e.Clones).Append(" why=").Append(e.Why)
                .Append(" by=").Append(e.By ?? "-").Append('\n');
        }

        return text.ToString();
    }
}
