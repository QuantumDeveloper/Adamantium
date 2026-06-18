using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using Adamantium.Mathematics.Triangulation;
using NUnit.Framework;

namespace Adamantium.MathTests
{
    /// <summary>
    /// Locks the triangulator's fill-rule behaviour (truth table, see plan §2) and the convex fast-path,
    /// so the Phase 0.5 perf work (convex fan, earcut later) can't silently change rendered results.
    /// </summary>
    public class FillRuleAndConvexTests
    {
        [Test]
        public void IsConvex_Classifies()
        {
            Assert.IsTrue(MathHelper.IsConvex(Rect(0, 0, 10, 10)), "square");
            Assert.IsTrue(MathHelper.IsConvex(new[] { V(0, 0), V(10, 0), V(5, 8) }), "triangle");
            Assert.IsTrue(MathHelper.IsConvex(Circle(32)), "circle");
            Assert.IsTrue(MathHelper.IsConvex(new[] { V(0, 0), V(5, 0), V(10, 0), V(10, 10), V(0, 10) }), "rect w/ collinear vertex");
            Assert.IsFalse(MathHelper.IsConvex(new[] { V(0, 0), V(10, 0), V(10, 10), V(5, 5), V(0, 10) }), "concave notch");
            Assert.IsFalse(MathHelper.IsConvex(Pentagram()), "pentagram (self-intersecting star)");
            Assert.IsFalse(MathHelper.IsConvex(new[] { V(0, 0), V(10, 0) }), "degenerate (2 pts)");
        }

        // Locks CURRENT behaviour: EvenOdd = real even-odd; NonZero currently drops inner contours and fills the
        // outermost solid (outer-contour semantics). (When a true non-zero winding lands, update these.)
        [Test]
        public void FillRule_TruthTable()
        {
            void Donut(bool oppWind, FillRule rule, bool centerFilled)
            {
                var p = new Polygon { FillRule = rule };
                p.AddContour(new MeshContour(Rect(0, 0, 100, 100)));
                p.AddContour(new MeshContour(oppWind ? RectCW(25, 25, 75, 75) : Rect(25, 25, 75, 75)));
                var tris = p.FillIndirect();
                Assert.AreEqual(centerFilled, Covered(tris, V(50, 50)), $"donut center rule={rule} opp={oppWind}");
                Assert.IsTrue(Covered(tris, V(10, 10)), $"donut ring rule={rule} opp={oppWind}");
            }

            Donut(false, FillRule.EvenOdd, false);
            Donut(true, FillRule.EvenOdd, false);
            Donut(false, FillRule.NonZero, true);          // NonZero drops the inner ring -> solid
            Donut(true, FillRule.NonZero, true);

            // Overlap of two same-wound squares: even-odd hollows it, NonZero fills it.
            void Overlap(FillRule rule, bool overlapFilled)
            {
                var p = new Polygon { FillRule = rule };
                p.AddContour(new MeshContour(Rect(0, 0, 60, 60)));
                p.AddContour(new MeshContour(Rect(40, 40, 100, 100)));
                var tris = p.FillIndirect();
                Assert.AreEqual(overlapFilled, Covered(tris, V(50, 50)), $"overlap rule={rule}");
                Assert.IsTrue(Covered(tris, V(10, 10)), $"A-only rule={rule}");
            }

            Overlap(FillRule.EvenOdd, false);
            Overlap(FillRule.NonZero, true);
        }

        [Test]
        public void Convex_FillsCorrectly_AndIsFan()
        {
            var sq = Fill(Rect(0, 0, 10, 10), FillRule.EvenOdd);
            Assert.IsTrue(Covered(sq, V(5, 5)), "square inside");
            Assert.IsFalse(Covered(sq, V(15, 5)), "square outside");
            Assert.AreEqual(2, sq.Count / 3, "square => n-2 = 2 triangles");

            var c = Fill(Circle(16), FillRule.EvenOdd);
            Assert.IsTrue(Covered(c, V(0, 0)), "circle center");
            Assert.IsFalse(Covered(c, V(200, 0)), "circle outside");
            Assert.AreEqual(14, c.Count / 3, "circle16 => n-2 = 14 triangles");
        }

        [Test]
        public void ConcaveSimple_EarcutFillsCorrectly()
        {
            // L-shape: bottom bar [0,20]x[0,10] + right bar [10,20]x[10,20]; top-left is a notch.
            var l = new[] { V(0, 0), V(20, 0), V(20, 20), V(10, 20), V(10, 10), V(0, 10) };
            Assert.IsFalse(MathHelper.IsConvex(l), "L is concave");

            var tris = Fill(l, FillRule.EvenOdd);
            Assert.IsTrue(Covered(tris, V(5, 5)), "bottom bar filled");
            Assert.IsTrue(Covered(tris, V(15, 15)), "right bar filled");
            Assert.IsFalse(Covered(tris, V(5, 15)), "notch empty");
            Assert.AreEqual(4, tris.Count / 3, "L (6 verts) => n-2 = 4 triangles (earcut)");
        }

        [Test]
        public void MultiContour_HoleViaEarcut()
        {
            // Donut (outer 0..100, inner 25..75): clean nesting -> earcut-with-holes path.
            // EvenOdd: ring filled, centre hole; quad + quad-hole earcuts to 8 triangles.
            var even = FillDonut(FillRule.EvenOdd);
            Assert.IsTrue(Covered(even, V(10, 10)), "ring filled");
            Assert.IsFalse(Covered(even, V(50, 50)), "centre hole");
            Assert.AreEqual(8, even.Count / 3, "quad with quad hole => 8 triangles");

            // NonZero (drops inner contour today): solid; outer quad => 2 triangles.
            var nz = FillDonut(FillRule.NonZero);
            Assert.IsTrue(Covered(nz, V(50, 50)), "hole filled solid");
            Assert.AreEqual(2, nz.Count / 3, "solid quad => 2 triangles");
        }

        static List<Vector3> FillDonut(FillRule rule)
        {
            var p = new Polygon { FillRule = rule };
            p.AddContour(new MeshContour(Rect(0, 0, 100, 100)));
            p.AddContour(new MeshContour(Rect(25, 25, 75, 75)));
            return p.FillIndirect();
        }

        // ---- helpers ----
        static Vector2 V(double x, double y) => new Vector2(x, y);
        static Vector2[] Rect(double x0, double y0, double x1, double y1) => new[] { V(x0, y0), V(x1, y0), V(x1, y1), V(x0, y1) };
        static Vector2[] RectCW(double x0, double y0, double x1, double y1) => new[] { V(x0, y0), V(x0, y1), V(x1, y1), V(x1, y0) };

        static Vector2[] Circle(int n, double r = 100)
        {
            var p = new Vector2[n];
            for (int i = 0; i < n; i++) { double a = 2 * Math.PI * i / n; p[i] = V(r * Math.Cos(a), r * Math.Sin(a)); }
            return p;
        }

        static Vector2[] Pentagram()
        {
            var o = Circle(5, 100);
            return new[] { o[0], o[2], o[4], o[1], o[3] };   // star {5/2}
        }

        static List<Vector3> Fill(Vector2[] pts, FillRule rule)
        {
            var p = new Polygon { FillRule = rule };
            p.AddContour(new MeshContour(pts));
            return p.FillIndirect();
        }

        static bool Covered(List<Vector3> tris, Vector2 pt)
        {
            for (int i = 0; i + 2 < tris.Count; i += 3)
                if (InTri(pt, (Vector2)tris[i], (Vector2)tris[i + 1], (Vector2)tris[i + 2])) return true;
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
}
