using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Core;

namespace Adamantium.UI.Rendering;

/// <summary>
/// Bakes a SIGNED DISTANCE FIELD for an arbitrary shape's boundary, so a halo on tessellated geometry is drawn by the
/// same pass as one on a rect or an ellipse - those compute the distance, this one reads it.
/// <para>Widening the analytic-AA ring instead would not work: offsetting a contour by a large distance is a Minkowski
/// sum, not a ring expansion. Its correct result changes topology - a star's notches close up once the offset passes the
/// local curvature radius - and a vertex-expanded ring cannot represent that. It also self-overlaps at every concave
/// corner, which double-blends a translucent band.</para>
/// <para>Baked in LOCAL space, so it survives resize and zoom, and cached per <c>GeometryKey</c> - meshes are shared, so
/// the cost is paid once per distinct shape. Quantisation only ever softens the BAND: the shape itself is still drawn
/// analytically, and a halo is blurry by definition.</para>
/// </summary>
internal static class HaloField
{
    /// <summary>Texels per side. Fixed rather than scaled to the shape: the field is sampled in normalised space, so a
    /// bigger shape simply spreads the same texels over more pixels - and a soft band hides that.</summary>
    public const int Resolution = 128;

    /// <summary>The distance range the field encodes, as a fraction of the shape's larger side. Generous on purpose: the
    /// band fades out as it approaches the range (a hard cut-off there draws a ghost of the baked box), so the range has
    /// to sit comfortably past any band an author would actually ask for, or the fade eats the glow itself.</summary>
    private const double PadFraction = 0.75;

    /// <summary>Bake the field as one byte per texel: 0.5 is exactly on the outline, below it inside, above it outside.
    /// <paramref name="pad"/> comes back in LOCAL units - the shader needs it to turn a sample back into a distance.</summary>
    public static byte[] Bake(List<(Vector2[] Points, bool IsClosed)> loops, Rect bounds, out double pad)
    {
        pad = Math.Max(bounds.Width, bounds.Height) * PadFraction;
        if (pad <= 0) pad = 1;

        var pixels = new byte[Resolution * Resolution];
        if (loops == null || loops.Count == 0)
        {
            return pixels;   // no boundary: everything reads as "far outside", i.e. no band
        }

        // The field covers the shape's box grown by the range on every side, so the outline sits well inside it.
        var minX = bounds.X - pad;
        var minY = bounds.Y - pad;
        var spanX = bounds.Width + pad * 2;
        var spanY = bounds.Height + pad * 2;

        for (var y = 0; y < Resolution; y++)
        {
            var py = minY + (y + 0.5) / Resolution * spanY;
            for (var x = 0; x < Resolution; x++)
            {
                var px = minX + (x + 0.5) / Resolution * spanX;
                var d = SignedDistance(loops, px, py);
                // 0.5 on the outline; the range maps to the full byte. Clamped, so a far texel is simply "far".
                var enc = 0.5 + Math.Clamp(d / pad, -1.0, 1.0) * 0.5;
                pixels[y * Resolution + x] = (byte)Math.Clamp(Math.Round(enc * 255.0), 0, 255);
            }
        }

        return pixels;
    }

    // Distance to the nearest boundary segment, signed by whether the point is inside. Brute force over every segment:
    // this runs ONCE per distinct shape, and a shape with enough segments for it to matter is rare in a UI.
    private static double SignedDistance(List<(Vector2[] Points, bool IsClosed)> loops, double px, double py)
    {
        var best = double.MaxValue;
        var inside = false;

        foreach (var (points, _) in loops)
        {
            if (points is not { Length: >= 2 }) continue;

            for (var i = 0; i < points.Length; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Length];

                var d = PointSegmentDistance(px, py, a.X, a.Y, b.X, b.Y);
                if (d < best) best = d;

                // Even-odd crossing: a ray to +X crosses this edge when the edge straddles py and the crossing is to
                // the right. Counted across ALL loops, so a shape with holes signs correctly too.
                if (a.Y > py != b.Y > py)
                {
                    var t = (py - a.Y) / (b.Y - a.Y);
                    if (px < a.X + t * (b.X - a.X)) inside = !inside;
                }
            }
        }

        if (best == double.MaxValue) return double.MaxValue;
        return inside ? -best : best;
    }

    private static double PointSegmentDistance(double px, double py, double ax, double ay, double bx, double by)
    {
        var vx = bx - ax;
        var vy = by - ay;
        var wx = px - ax;
        var wy = py - ay;

        var lenSq = vx * vx + vy * vy;
        var t = lenSq > 1e-12 ? Math.Clamp((wx * vx + wy * vy) / lenSq, 0.0, 1.0) : 0.0;

        var dx = wx - t * vx;
        var dy = wy - t * vy;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
