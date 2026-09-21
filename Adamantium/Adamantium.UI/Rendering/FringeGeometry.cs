using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.UI.Rendering.RenderUnits;

namespace Adamantium.UI.Rendering;

/// <summary>
/// The analytic-AA fringe's geometry, independent of who draws it: which way each fill contour must feather (its
/// <c>Winding</c>) and the ring of triangles around it. Both the per-unit fringe (<c>GpuFillRenderComponent</c>) and the
/// instanced one (<c>InstancedFillCollector</c>, one ring shared per <c>GeometryKey</c>) build from here, so a hole's
/// inward feather is decided in ONE place.
/// </summary>
/// <remarks>
/// The ring carries no width: a vertex holds the contour point plus, for the outer edge, the two adjacent edge
/// DIRECTIONS, and the vertex shader turns those into a screen-space miter one device pixel long (FillFringeEffect.fx /
/// BatchEffect.fx). That is what makes the ring scale-free, hence shareable across every instance of a mesh.
/// </remarks>
internal static class FringeGeometry
{
    /// <summary>Each drawable contour (>= 3 points) with the sign that makes its fringe feather AWAY from the fill.</summary>
    public static List<(Vector2[] Points, float Winding)> Build(IReadOnlyList<(Vector2[] Points, bool IsClosed)> contours)
    {
        List<(Vector2[] Points, float Winding)> built = [];
        foreach (var (points, _) in contours)
        {
            if (points is not { Length: >= 3 }) continue;   // a fill contour needs at least a triangle
            // Outward miter sign from the contour's signed area (screen space is y-down, so the sign is tuned by the
            // headless edge test: the fringe must land OUTSIDE the shape).
            built.Add((points, SignedArea(points) >= 0 ? -1f : 1f));
        }
        ApplyNesting(built);
        return built;
    }

    /// <summary>The ring's triangles for one mesh's contours, laid out as ONE vertex list (6 verts per contour segment,
    /// contours back to back) so a shared ring is a single draw.</summary>
    public static FringeVertex[] BuildRing(List<(Vector2[] Points, float Winding)> contours)
    {
        var total = 0;
        foreach (var (points, _) in contours) total += points.Length * 6;
        var verts = new FringeVertex[total];

        var v = 0;
        foreach (var (points, winding) in contours)
        {
            var n = points.Length;
            for (var i = 0; i < n; i++)
            {
                var ni = (i + 1) % n;
                var a = new Vector2F((float)points[i].X, (float)points[i].Y);
                var b = new Vector2F((float)points[ni].X, (float)points[ni].Y);
                EdgeDirs(points, i, winding, out var a0, out var a1);
                EdgeDirs(points, ni, winding, out var b0, out var b1);

                // Two triangles between the contour edge (inner, no directions -> never offset) and the outer edge.
                verts[v++] = Inner(a);
                verts[v++] = Outer(a, a0, a1);
                verts[v++] = Inner(b);
                verts[v++] = Outer(a, a0, a1);
                verts[v++] = Outer(b, b0, b1);
                verts[v++] = Inner(b);
            }
        }
        return verts;
    }

    private static FringeVertex Inner(Vector2F pos) => new() { Position = pos };

    private static FringeVertex Outer(Vector2F pos, Vector2F d0, Vector2F d1) => new() { Position = pos, Dir0 = d0, Dir1 = d1 };

    // The two adjacent edge directions at contour point i, with Winding folded into their sign (reversing a direction
    // reverses its 90-degree normal, which is how a hole feathers inward). Closed loop => i always has both neighbours.
    private static void EdgeDirs(Vector2[] points, int i, float winding, out Vector2F d0, out Vector2F d1)
    {
        var n = points.Length;
        var prev = (i + n - 1) % n;
        var next = (i + 1) % n;
        d0 = SafeDir(points[i] - points[prev]) * winding;
        d1 = SafeDir(points[next] - points[i]) * winding;
    }

    private static Vector2F SafeDir(Vector2 v)
    {
        var len = Math.Sqrt(v.X * v.X + v.Y * v.Y);
        if (len <= 1e-9) return Vector2F.Zero;   // duplicate contour points would otherwise produce a NaN direction
        return new Vector2F((float)(v.X / len), (float)(v.Y / len));
    }

    // Even-odd nesting: a contour inside an ODD number of the others is a HOLE - the fill is OUTSIDE it, so its fringe
    // must feather INWARD (toward the hole), opposite an outer contour. Winding alone can't tell them apart (the
    // tessellator emits holes with the same winding as outers), so flip holes here. A probe VERTEX is used, not the
    // centroid - a frame-shaped outer's centroid can fall inside its own hole and misclassify it.
    private static void ApplyNesting(List<(Vector2[] Points, float Winding)> built)
    {
        for (var i = 0; i < built.Count; i++)
        {
            var nesting = 0;
            for (var j = 0; j < built.Count; j++)
                if (j != i && PointInPolygon(built[i].Points[0], built[j].Points))
                    nesting++;
            if ((nesting & 1) == 1) built[i] = (built[i].Points, -built[i].Winding);
        }
    }

    // Ray-cast point-in-polygon (used to find a contour's even-odd nesting depth -> hole vs outer).
    private static bool PointInPolygon(Vector2 p, Vector2[] poly)
    {
        var inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].Y > p.Y) != (poly[j].Y > p.Y) &&
                p.X < (poly[j].X - poly[i].X) * (p.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X)
                inside = !inside;
        }
        return inside;
    }

    private static double SignedArea(Vector2[] p)
    {
        double a = 0;
        for (int i = 0, n = p.Length; i < n; i++)
        {
            var j = (i + 1) % n;
            a += p[i].X * p[j].Y - p[j].X * p[i].Y;
        }
        return a * 0.5;
    }
}
