using System;

namespace Adamantium.UI.Rendering;

// SCRATCH (phase 0 of §5a): does the layer key hold? A LAYER is one FlushBatches cycle - the set of draws whose mutual
// order is irrelevant - and the doc's one pre-condition is that layers are no more numerous than today's segments, or the
// number of draw calls would grow. Also counts the repairs the redesign is meant to make unnecessary.
public static class LayerProbe
{
    public static long Frames, Cycles, Segments, MaxCycles, MaxSegments;
    public static long Splits, SplitsAvoided, Renumbers, Refusals;

    // Phase 3: how often a control leaving the paint order costs a pass over the arena, and how many slots that pass
    // covered. The phase is verified on these being small and rare - a sweep per hidden control, not per frame.
    public static long OrphanSweeps, OrphanSweptSlots;

    private static long _cyclesThisFrame, _segmentsThisFrame;

    public static void FrameStart()
    {
        if (_cyclesThisFrame > 0 || _segmentsThisFrame > 0)
        {
            Frames++;
            Cycles += _cyclesThisFrame;
            Segments += _segmentsThisFrame;
            if (_cyclesThisFrame > MaxCycles) MaxCycles = _cyclesThisFrame;
            if (_segmentsThisFrame > MaxSegments) MaxSegments = _segmentsThisFrame;
        }

        _cyclesThisFrame = 0;
        _segmentsThisFrame = 0;
    }

    /// <summary>Zero everything - for a test that asks a question about ONE frame it drives itself.</summary>
    public static void Reset()
    {
        Frames = Cycles = Segments = MaxCycles = MaxSegments = 0;
        Splits = SplitsAvoided = Renumbers = Refusals = 0;
        OrphanSweeps = OrphanSweptSlots = 0;
        _cyclesThisFrame = _segmentsThisFrame = 0;
    }

    public static void Cycle() => _cyclesThisFrame++;
    public static void Segment() => _segmentsThisFrame++;

    public static string Dump()
    {
        var f = Math.Max(1, Frames);
        return $"layers/frame {Cycles / (double)f:0.0} (max {MaxCycles}) | segments/frame {Segments / (double)f:0.0} (max {MaxSegments})"
               + $" | recorded frames {Frames} | splits {Splits} (avoided {SplitsAvoided}) | renumbers {Renumbers} | patch refusals {Refusals}"
               + $" | orphan sweeps {OrphanSweeps} over {OrphanSweptSlots} slots";
    }
}
