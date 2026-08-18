using System;

namespace Adamantium.UI.Rendering;

// SCRATCH (phase 0 of §5a): does the layer key hold? A LAYER is one FlushBatches cycle - the set of draws whose mutual
// order is irrelevant - and the doc's one pre-condition is that layers are no more numerous than today's segments, or the
// number of draw calls would grow. Also counts the repairs the redesign is meant to make unnecessary.
public static class LayerProbe
{
    public static long Frames, Cycles, Segments, MaxCycles, MaxSegments;
    public static long Splits, Renumbers, Refusals;

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

    public static void Cycle() => _cyclesThisFrame++;
    public static void Segment() => _segmentsThisFrame++;

    public static string Dump()
    {
        var f = Math.Max(1, Frames);
        return $"layers/frame {Cycles / (double)f:0.0} (max {MaxCycles}) | segments/frame {Segments / (double)f:0.0} (max {MaxSegments})"
               + $" | recorded frames {Frames} | splits {Splits} | renumbers {Renumbers} | patch refusals {Refusals}";
    }
}
