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
    /// <summary>The shader's pattern/noise type: PatternType as-is, 4 = simplex FBM, 7/8/9 = perlin/value/worley.</summary>
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

    private PatternBrushRecord(int type, Color color1, Color color2, Color midColor, double cell, double opacity, Vector4F noise)
    {
        Type = type;
        Color1 = color1;
        Color2 = color2;
        MidColor = midColor;
        Cell = cell;
        Opacity = opacity;
        Noise = noise;
    }

    public static bool TryDescribe(Brush brush, out PatternBrushRecord record)
    {
        record = default;

        if (brush is PatternBrush pat)
        {
            // Bake the hatch line normal (cos/sin) here so the shader needs NO trig (its pattern PS is at the NVVM limit).
            var ha = pat.HatchAngle * Math.PI / 180.0;
            record = new PatternBrushRecord((int)pat.Pattern, pat.Color1, pat.Color2, new Color(0, 0, 0, 0),
                pat.CellSize, pat.Opacity, new Vector4F((float)Math.Cos(ha), (float)Math.Sin(ha), 0, 0));
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

            record = new PatternBrushRecord(n.NoiseType == NoiseType.Simplex ? 4 : 6 + (int)n.NoiseType,
                n.Color1, n.Color2, n.MidColor, n.Scale, n.Opacity, noise);
            return true;
        }

        return false;
    }
}
