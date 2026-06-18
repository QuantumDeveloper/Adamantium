using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using NUnit.Framework;

namespace Adamantium.UITests;

// Regression tests for CombinedGeometry boolean modes (Union / Intersect / Exclude / Xor). Pure CPU path
// (ProcessGeometryCore -> intersection/merge -> triangulate); no GPU. Guards the mode fixes:
//  - disjoint shapes now respect the mode (Intersect = empty, Exclude = Geometry1 only) instead of always unioning.
public class CombinedGeometryTests
{
    // ---- disjoint inputs (bounding boxes don't overlap) -> the fixed else-branch ----

    [Test]
    public void Disjoint_Union_CoversBoth()
    {
        var (a, b) = (new Vector2(20, 20), new Vector2(120, 20));
        var m = Combine(GeometryCombineMode.Union, DisjointA(), DisjointB());
        Assert.IsTrue(Covered(m, a), "G1 covered");
        Assert.IsTrue(Covered(m, b), "G2 covered");
    }

    [Test]
    public void Disjoint_Intersect_IsEmpty()
    {
        var m = Combine(GeometryCombineMode.Intersect, DisjointA(), DisjointB());
        Assert.IsFalse(Covered(m, new Vector2(20, 20)), "G1 not covered (empty intersection)");
        Assert.IsFalse(Covered(m, new Vector2(120, 20)), "G2 not covered (empty intersection)");
    }

    [Test]
    public void Disjoint_Exclude_KeepsGeometry1Only()
    {
        var m = Combine(GeometryCombineMode.Exclude, DisjointA(), DisjointB());
        Assert.IsTrue(Covered(m, new Vector2(20, 20)), "G1 kept");
        Assert.IsFalse(Covered(m, new Vector2(120, 20)), "G2 removed");
    }

    [Test]
    public void Disjoint_Xor_CoversBoth()
    {
        var m = Combine(GeometryCombineMode.Xor, DisjointA(), DisjointB());
        Assert.IsTrue(Covered(m, new Vector2(20, 20)), "G1 covered");
        Assert.IsTrue(Covered(m, new Vector2(120, 20)), "G2 covered");
    }

    // ---- overlapping inputs -> the intersection/merge path ----
    // A = [0,60]x[0,60], B = [40,100]x[40,100]; overlap = [40,60]x[40,60].

    [Test]
    public void Overlap_Union_CoversAOnlyBOnlyAndOverlap()
    {
        var m = Combine(GeometryCombineMode.Union, OverlapA(), OverlapB());
        Assert.IsTrue(Covered(m, new Vector2(10, 10)), "A-only");
        Assert.IsTrue(Covered(m, new Vector2(90, 90)), "B-only");
        Assert.IsTrue(Covered(m, new Vector2(50, 50)), "overlap");
        Assert.IsFalse(Covered(m, new Vector2(150, 150)), "exterior empty");
    }

    [Test]
    public void Overlap_Intersect_CoversOnlyOverlap()
    {
        var m = Combine(GeometryCombineMode.Intersect, OverlapA(), OverlapB());
        Assert.IsTrue(Covered(m, new Vector2(50, 50)), "overlap covered");
        Assert.IsFalse(Covered(m, new Vector2(10, 10)), "A-only not covered");
        Assert.IsFalse(Covered(m, new Vector2(90, 90)), "B-only not covered");
    }

    [Test]
    public void Overlap_Exclude_CoversAMinusB()
    {
        var m = Combine(GeometryCombineMode.Exclude, OverlapA(), OverlapB());
        Assert.IsTrue(Covered(m, new Vector2(10, 10)), "A-only kept");
        Assert.IsFalse(Covered(m, new Vector2(50, 50)), "overlap removed");
        Assert.IsFalse(Covered(m, new Vector2(90, 90)), "B-only not added");
    }

    [Test]
    public void Overlap_Xor_CoversSymmetricDifference()
    {
        var m = Combine(GeometryCombineMode.Xor, OverlapA(), OverlapB());
        Assert.IsTrue(Covered(m, new Vector2(10, 10)), "A-only");
        Assert.IsTrue(Covered(m, new Vector2(90, 90)), "B-only");
        Assert.IsFalse(Covered(m, new Vector2(50, 50)), "overlap excluded");
    }

    // Regression: nested ellipses (one fully inside the other) combined into a solid shape must fill cleanly.
    // Before coordinate snapping, floating-point-noise-equal points were treated as distinct and the triangulator
    // cut spurious strips/bands through the disc -> some interior samples were left uncovered.
    [Test]
    public void NestedEllipses_Union_FillsWithoutStrips()
    {
        var outer = new EllipseGeometry(new Vector2(100, 100), 80, 80);
        var inner = new EllipseGeometry(new Vector2(100, 100), 40, 40);
        var m = Combine(GeometryCombineMode.Union, outer, inner);

        var offsets = new[] { (0, 0), (0, -60), (0, 60), (-60, 0), (60, 0), (0, -20), (0, 20), (35, 35), (-35, -35) };
        foreach (var (dx, dy) in offsets)
            Assert.IsTrue(Covered(m, new Vector2(100 + dx, 100 + dy)), $"interior point ({dx},{dy}) must be filled (no strip)");
    }

    // ---- helpers ----
    static RectangleGeometry DisjointA() => new(new Rect(0, 0, 40, 40));
    static RectangleGeometry DisjointB() => new(new Rect(100, 0, 40, 40));
    static RectangleGeometry OverlapA() => new(new Rect(0, 0, 60, 60));
    static RectangleGeometry OverlapB() => new(new Rect(40, 40, 60, 60));

    static Mesh Combine(GeometryCombineMode mode, Geometry g1, Geometry g2)
    {
        var combined = new CombinedGeometry { Geometry1 = g1, Geometry2 = g2, GeometryCombineMode = mode };
        combined.ProcessGeometry(GeometryType.Both);
        return combined.Mesh;
    }

    static bool Covered(Mesh mesh, Vector2 p)
    {
        var pts = mesh.Points;
        for (int i = 0; i + 2 < pts.Length; i += 3)
            if (InTri(p, (Vector2)pts[i], (Vector2)pts[i + 1], (Vector2)pts[i + 2])) return true;
        return false;
    }

    static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        double d1 = Sgn(p, a, b), d2 = Sgn(p, b, c), d3 = Sgn(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
    }

    static double Sgn(Vector2 p, Vector2 a, Vector2 b) => (p.X - b.X) * (a.Y - b.Y) - (a.X - b.X) * (p.Y - b.Y);
}
