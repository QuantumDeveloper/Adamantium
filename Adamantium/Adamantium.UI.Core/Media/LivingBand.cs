using Adamantium.Mathematics;

namespace Adamantium.UI.Core.Media;

/// <summary>
/// A LIVING aura as the renderer wants it: the same band as <see cref="HaloBand"/>, plus how far its reach wanders,
/// how fast that drifts, how fine the tongues are, and the palette the wander travels.
/// <para>Kept apart from the plain band rather than folded into it: only an aura can live - a shadow cast by an object
/// does not breathe - and a still band must not carry a palette it never reads, nor pay for a pass it never needs.</para>
/// <para>An immutable value captured on the RECORD thread, like every other halo value.</para>
/// </summary>
public readonly struct LivingBand
{
    public LivingBand(float spread, float softness, bool inner, float turbulence, float flow, float detail,
        Vector4F color, Vector4F[] palette, float[] offsets, int stopCount)
    {
        Color = color;
        Spread = spread;
        Softness = softness;
        Inner = inner;
        Turbulence = turbulence;
        Flow = flow;
        Detail = detail;
        Palette = palette;
        Offsets = offsets;
        StopCount = stopCount;
    }

    /// <summary>Straight-alpha RGBA with the author's Opacity folded in - what the band paints when the palette is empty.</summary>
    public Vector4F Color { get; }

    public float Spread { get; }

    public float Softness { get; }

    public bool Inner { get; }

    /// <summary>How far the reach wanders, as a fraction of the softness. Zero would be a still band - which is drawn by
    /// the plain pass instead, so it never reaches here.</summary>
    public float Turbulence { get; }

    /// <summary>How fast the wander drifts. Zero holds it still: the same uneven glow, frozen.</summary>
    public float Flow { get; }

    /// <summary>How many tongues run around the outline.</summary>
    public float Detail { get; }

    /// <summary>The colour ramp, packed by the SAME packer gradients use. Null when the aura carries a single colour.</summary>
    public Vector4F[] Palette { get; }

    public float[] Offsets { get; }

    /// <summary>Valid entries in <see cref="Palette"/>; 0 means "use the aura's own colour".</summary>
    public int StopCount { get; }

    /// <summary>How far past the outline this band can reach at its widest - the wander adds to the reach, so the drawn
    /// quad has to hold more than a still band of the same radius would.</summary>
    public float Reach => Spread + Softness * (1.0f + Turbulence);
}
