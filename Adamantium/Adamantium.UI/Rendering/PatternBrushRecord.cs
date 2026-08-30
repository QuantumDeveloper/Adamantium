using System;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Media;

namespace Adamantium.UI.Rendering;

/// <summary>
/// THE one translation of a <see cref="PatternBrush"/> / <see cref="NoiseBrush"/> into the fields the pattern shader
/// reads. Both bakes go through it - the SDF rect/ellipse batch and the instanced fill for arbitrary geometry - because
/// they used to state it TWICE and the copies drifted: the geometry one never packed the hatch normal, so a hatch on a
/// Polygon came out with a direction of (0,0) and drew nothing, while the same brush on a rectangle was fine. The same
/// shape of bug had already cost hours in the gradient family.
/// </summary>
internal readonly struct PatternBrushRecord
{
    /// <summary>The kind code the shader keys its pass on: a <see cref="PatternType"/> as-is, or a
    /// <see cref="NoiseType"/> offset by <see cref="NoiseBase"/>. Produced ONLY here - see the note there.</summary>
    public readonly int Type;

    public readonly Color Color1;
    public readonly Color Color2;

    /// <summary>The gradient-map MID colour (NoiseBrush only); transparent = off.</summary>
    public readonly Color MidColor;

    /// <summary>PatternBrush.CellSize / NoiseBrush.Scale - the caller decides whether it stays in local units or scales
    /// to device pixels.</summary>
    public readonly double Cell;

    public readonly double Opacity;

    /// <summary>The type-specific pack: a hatch's baked line normal (cos, sin) so the shader needs no trig, or a noise's
    /// (octaves, seed, lacunarity, gain) with the animate flag in the SIGN of octaves and, for CombustibleVoronoi, the
    /// palette flag in .w.</summary>
    public readonly Vector4F Noise;

    /// <summary>The phase a PAUSED noise flows at - the brush's own phase captured when Animate went off, so a pause holds the
    /// frame it stopped on instead of snapping back to the start.</summary>
    public readonly double FrozenPhase;

    /// <summary>Subtracted from the live clock while ANIMATING, so the brush flows on its own phase rather than the shared
    /// one. It only changes when animation is switched on, so an animated instance still replays without a re-bake.</summary>
    public readonly double PhaseOffset;

    private PatternBrushRecord(int type, Color color1, Color color2, Color midColor, double cell, double opacity, Vector4F noise, double frozenPhase, double phaseOffset)
    {
        FrozenPhase = frozenPhase;
        PhaseOffset = phaseOffset;
        Type = type;
        Color1 = color1;
        Color2 = color2;
        MidColor = midColor;
        Cell = cell;
        Opacity = opacity;
        Noise = noise;
    }


    // BOTH families ride ONE field of the record (Params.y) through ONE collector, so their codes must not collide -
    // and THIS is the only place that knows it. The public enums stay naturally numbered from zero: PatternType has no
    // holes punched in it for noise, NoiseType none for patterns, and adding a kind to either never renumbers the other.
    //
    // Noise sits in its own hundred. Not a clever bit-packing: a reader who sees 103 in a capture can tell instantly
    // which family it belongs to, and the shader's pass table splits into two obvious ranges instead of one interleaved
    // list where 4 was noise and 5 was a pattern.
    public const int NoiseBase = 100;

    private static int PatternCode(PatternType pattern) => (int)pattern;

    private static int NoiseCode(NoiseType noise) => NoiseBase + (int)noise;

    public static bool TryDescribe(Brush brush, out PatternBrushRecord record)
    {
        record = default;

        if (brush is PatternBrush pat)
        {
            // Bake the hatch line normal (cos/sin) here so the shader needs NO trig (its pattern PS is at the NVVM limit).
            var ha = pat.HatchAngle * Math.PI / 180.0;
            record = new PatternBrushRecord(PatternCode(pat.Pattern), pat.Color1, pat.Color2, new Color(0, 0, 0, 0),
                pat.CellSize, pat.Opacity, new Vector4F((float)Math.Cos(ha), (float)Math.Sin(ha), 0, 0), 0.0, 0.0);
            return true;
        }

        if (brush is NoiseBrush n)
        {
            // Animate flag packed into the SIGN of octaves (no spare slot): negative = advance by the shared Time.
            var octEnc = n.Animate ? -(float)Math.Max(1, n.Octaves) : n.Octaves;
            var noise = new Vector4F(octEnc, (float)n.Seed, (float)n.Lacunarity, (float)n.Gain);
            // CombustibleVoronoi ignores lacunarity/gain, so reuse .w as the palette flag (1 = fire, 0 = the brush's own ramp).
            if (n.NoiseType == NoiseType.CombustibleVoronoi)
            {
                noise.W = n.UseFirePalette ? 1f : 0f;
            }

            record = new PatternBrushRecord(NoiseCode(n.NoiseType),
                n.Color1, n.Color2, n.MidColor, n.Scale, n.Opacity, noise, n.FrozenPhase, n.PhaseOffset);
            return true;
        }

        return false;
    }
}
