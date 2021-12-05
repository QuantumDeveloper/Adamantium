using System.Collections.Generic;
using System.Diagnostics;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.MathTests
{
    public class MathTests
    {
        [Test]
        public void ConcaveTest()
        {
            List<Vector3> points = new List<Vector3>();
            points.Add(new Vector3(0, 0, 0));
            points.Add(new Vector3(0, 20, 0));
            points.Add(new Vector3(10, 10, 0));
            points.Add(new Vector3(20, 20, 0));
            points.Add(new Vector3(20, 0, 0));
            var timer = Stopwatch.StartNew();
            Assert.IsTrue(PolygonHelper.IsPolygonConcave(points), "polygon is not concave");
            timer.Stop();
        }

        [Test]
        public void Concave2Test()
        {
            var points = new List<Vector2>();
            points.Add(new Vector2(0, 0));
            points.Add(new Vector2(0, 20));
            points.Add(new Vector2(10, 10));
            points.Add(new Vector2(20, 20));
            points.Add(new Vector2(20, 0));

            Polygon polygon = new Polygon();
            polygon.AddItem(new PolygonItem(points));

            var timer = Stopwatch.StartNew();
            Assert.IsTrue(PolygonHelper.IsPolygonConcave(polygon.MergedSegments), "polygon is not concave");
            timer.Stop();
        }

        [Test]
        public void ConvexTest()
        {
            List<Vector3> points = new List<Vector3>();
            points.Add(new Vector3(0, 0, 0));
            points.Add(new Vector3(0, 20, 0));
            points.Add(new Vector3(10, 20, 0));
            points.Add(new Vector3(20, 20, 0));
            points.Add(new Vector3(20, 0, 0));
            points.Add(new Vector3(0, 0, 0));
            var timer = Stopwatch.StartNew();
            Assert.IsFalse(PolygonHelper.IsPolygonConcave(points), "polygon is not convex");
            timer.Stop();

        }

        [Test]
        public void PolygonHasNoSelfIntersections()
        {
            var points = new List<Vector2>();
            points.Add(new Vector2(0, 0));
            points.Add(new Vector2(0, 20));
            points.Add(new Vector2(10, 20));
            points.Add(new Vector2(20, 20));
            points.Add(new Vector2(20, 0));
            points.Add(new Vector2(0, 0));
            PolygonItem p = new PolygonItem(points);
            p.SplitOnSegments();
            var hasSelfIntersections = p.CheckForSelfIntersection(FillRule.NonZero);
            Assert.IsTrue(!hasSelfIntersections, "polygon has selfintersections");
        }

        [Test]
        public void PolygonHasSelfIntersections()
        {
            var points = new List<Vector2>();
            points.Add(new Vector2(-10, 0));
            points.Add(new Vector2(0, 30));
            points.Add(new Vector2(10, 0));
            points.Add(new Vector2(-15, 15));
            points.Add(new Vector2(15, 15));
            PolygonItem p = new PolygonItem(points);
            p.SplitOnSegments();
            var hasSelfIntersections = p.CheckForSelfIntersection(FillRule.NonZero);
            Assert.IsTrue(hasSelfIntersections, "polygon has no selfintersections");
            Assert.AreEqual(p.SelfIntersectedPoints.Count, 5);
        }

        [Test]
        public void IsPointOnLineTest()
        {
            var segment = new LineSegment2D(new Vector2(0, 0), new Vector2(10, 0));
            var point = new Vector2(5, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsTrue(isOnLine, "point is not on the line");
        }

        [Test]
        public void IsPointOnLeftSegmentEdgeTest()
        {
            var segment = new LineSegment2D(new Vector2(0, 0), new Vector2(10, 0));
            var point = new Vector2(0, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsTrue(isOnLine, "point is not on the line");
        }

        [Test]
        public void IsPointOnRightSegmentEdgeTest()
        {
            var segment = new LineSegment2D(new Vector2(0, 0), new Vector2(10, 0));
            var point = new Vector2(10, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsTrue(isOnLine, "point is not on the line");
        }

        [Test]
        public void IsPointNotOnSegmentTest2()
        {
            var segment = new LineSegment2D(new Vector2(0, 0), new Vector2(10, 10));
            var point = new Vector2(5, 5.01f);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsFalse(isOnLine, "point is on the line");
        }

        [Test]
        public void IsPointNotOnLineTest()
        {
            var segment = new LineSegment2D(new Vector2(0, 0), new Vector2(10, 0));
            var point = new Vector2(11, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsFalse(isOnLine, "point is on the line");
        }

        [Test]
        public void IsPointNotOnSegmentTest()
        {
            var segment = new LineSegment2D(new Vector2(0, 0), new Vector2(0, 20));
            var point = new Vector2(5, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsFalse(isOnLine, "point is on the line");
        }

        [Test]
        public void SegmentSegmentIntersectionTest()
        {
            var segment1 = new LineSegment2D(new Vector2(-10, 0), new Vector2(0, 30));
            var segment2 = new LineSegment2D(new Vector2(-15, 15), new Vector2(15, 15));

            var isLineIntersects = Collision2D.SegmentSegmentIntersection(ref segment1, ref segment2, out var intersectionPoint);
            Assert.IsTrue(isLineIntersects, "lines is not intersecting");
            Assert.AreEqual(new Vector2(-5, 15), intersectionPoint);
        }


        [Test]
        public void RayLineIntersectionTest()
        {
            LineSegment2D s = new LineSegment2D(new Vector2(1, 1), new Vector2(10, 1));
            var ray = new Ray2D(new Vector2(2, 10), new Vector2(0, -1));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out var intersectionPoint);
            Assert.IsTrue(isLineIntersects, "lines is not intersecting");
            Assert.AreEqual(new Vector2(2, 1), intersectionPoint);
        }

        [Test]
        public void RayLineIntersectionTest2()
        {
            LineSegment2D s = new LineSegment2D(new Vector2(-20, 8), new Vector2(10, 1));
            var ray = new Ray2D(new Vector2(-2, 10), new Vector2(0, -1));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out var intersectionPoint);
            Assert.IsTrue(isLineIntersects, "ray and line is not intersecting");
            Assert.AreEqual(new Vector2(-2, 3.8), intersectionPoint);
        }

        [Test]
        public void CollinearRayLineIntersection()
        {
            LineSegment2D s = new LineSegment2D(new Vector2(20, 0), new Vector2(30, 0));
            var ray = new Ray2D(new Vector2(1, 0), new Vector2(1, 0));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out var intersectionPoint);
            Assert.IsFalse(isLineIntersects, "ray and line is not collinear");
        }

        [Test]
        public void CollinearRayLineIntersection2()
        {
            LineSegment2D s = new LineSegment2D(new Vector2(20, 0), new Vector2(10, 0));
            var ray = new Ray2D(new Vector2(1, 0), new Vector2(1, 0));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out var intersectionPoint);
            Assert.IsFalse(isLineIntersects, "ray and line is not collinear");
        }
        
        [Test]
        public void CollinearRayLineIntersection3()
        {
            LineSegment2D s = new LineSegment2D(new Vector2(350.5, 1802.5), new Vector2(449.777, 1811.66663));
            var ray = new Ray2D(new Vector2(1, 1802.5), new Vector2(1, 0));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out var intersectionPoint);
            Assert.IsTrue(isLineIntersects, "ray and line is not collinear");

            ray.Origin = new Vector2(1, 1811.66663);
            isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out intersectionPoint);
            Assert.IsTrue(isLineIntersects, "ray and line is not collinear");
        }
        
        [Test]
        public void CollinearRayLineIntersection4()
        {
            LineSegment2D s = new LineSegment2D(new Vector2(468.5, 1802.5), new Vector2(485.61111, 1790.27783));
            var ray = new Ray2D(new Vector2(1, 1802.5), new Vector2(1, 0));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out var intersectionPoint);
            Assert.IsTrue(isLineIntersects, "ray and line is not collinear");

            ray.Origin = new Vector2(1, 1790.27783);
            isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out intersectionPoint);
            Assert.IsTrue(isLineIntersects, "ray and line is not collinear");
        }

        [Test]
        public void RayWithStartOfLineIntersection()
        {
            LineSegment2D s = new LineSegment2D(new Vector2(20, 0), new Vector2(30, 0));
            var ray = new Ray2D(new Vector2(20, 10), new Vector2(0, -1));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out var intersectionPoint);
            Assert.IsTrue(isLineIntersects, "ray and line is not intersecting");
            Assert.AreEqual(new Vector2(20, 0), intersectionPoint);
        }

        [Test]
        public void RayWithEndOfLineIntersection()
        {
            LineSegment2D s = new LineSegment2D(new Vector2(20, 0), new Vector2(30, 0));
            var ray = new Ray2D(new Vector2(30, 10), new Vector2(0, -1));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out var intersectionPoint);
            Assert.IsTrue(isLineIntersects, "ray and line is not intersecting");
            Assert.AreEqual(new Vector2(30, 0), intersectionPoint);
        }


        [Test]
        public void TriangulationTest()
        {
            var points = new List<Vector2>();
            //points.Add(new Vector3D(0, 0, 0));
            //points.Add(new Vector3D(0, 20, 0));
            //points.Add(new Vector3D(10, 10, 0));
            //points.Add(new Vector3D(20, 20, 0));
            //points.Add(new Vector3D(20, 0, 0));

            //points.Add(new Vector3D(0, 0, 0));
            //points.Add(new Vector3D(0, 20, 0));
            //points.Add(new Vector3D(5, 30, 0));
            //points.Add(new Vector3D(5, 20, 0));
            //points.Add(new Vector3D(10, 20, 0));
            //points.Add(new Vector3D(10, 30, 0));
            //points.Add(new Vector3D(20, 20, 0));
            //points.Add(new Vector3D(20, 0, 0));

            //points.Add(new Vector3D(-10, 0, 0));
            //points.Add(new Vector3D(0, 30, 0));
            //points.Add(new Vector3D(10, 0, 0));
            //points.Add(new Vector3D(-15, 15, 0));
            //points.Add(new Vector3D(15, 15, 0));



            //Double star
            /*
            points.Add(new Vector3D(-10, 0, 0));
            points.Add(new Vector3D(0, 30, 0));
            points.Add(new Vector3D(10, 0, 0));
            points.Add(new Vector3D(-15, 15, 0));
            points.Add(new Vector3D(15, 15, 0));

            points.Add(new Vector3D(-10, 0, 0));
            points.Add(new Vector3D(0, -30, 0));
            points.Add(new Vector3D(10, 0, 0));
            points.Add(new Vector3D(-15, -15, 0));
            points.Add(new Vector3D(15, 15, 0));
            */

            Polygon polygon = new Polygon();
            /*
            points.Add(new Vector3D(-10, 0, 0));
            points.Add(new Vector3D(0, 30, 0));
            points.Add(new Vector3D(10, 0, 0));
            points.Add(new Vector3D(-15, 15, 0));
            points.Add(new Vector3D(15, 15, 0));
            polygon.Polygons.Add(new PolygonItem(points));

            var points1 = new List<Vector3D>();
            points1.Add(new Vector3D(-10, 0, 0));
            points1.Add(new Vector3D(0, -30, 0));
            points1.Add(new Vector3D(10, 0, 0));
            points1.Add(new Vector3D(-15, -15, 0));
            points1.Add(new Vector3D(15, -15, 0));

            //polygon.Polygons.Add(new PolygonItem(points1));
            var points2 = new List<Vector3D>();

            points2.Add(new Vector3D(-4, 14, 0));
            points2.Add(new Vector3D(4, 14, 0));
            points2.Add(new Vector3D(4, 10, 0));
            points2.Add(new Vector3D(-4, 10, 0));

            polygon.Polygons.Add(new PolygonItem(points2));
            */

            points.Add(new Vector2(-10, 0));
            points.Add(new Vector2(0, 30));
            points.Add(new Vector2(10, 0));
            points.Add(new Vector2(-15, 15));
            points.Add(new Vector2(15, 15));

            points.Add(new Vector2(-10, 0));
            points.Add(new Vector2(0, -30));
            points.Add(new Vector2(10, 0));
            points.Add(new Vector2(-15, -15));
            points.Add(new Vector2(15, -15));

            polygon.AddItem(new PolygonItem(points));

            polygon.FillRule = FillRule.NonZero;
            var result = polygon.Fill();

        }

        [Test]
        public void IsClockwise2D()
        {
            Assert.IsTrue(MathHelper.IsClockwise(new Vector2(10, 0), new Vector2(10, 20), new Vector2(0, 20)), "triangle is counter clockwise");
        }


        [Test]
        public void IsCounterClockwise2D()
        {
            Assert.IsFalse(MathHelper.IsClockwise(new Vector2(0, 20), new Vector2(10, 30), new Vector2(10, 20)), "triangle is clockwise");
        }

        [Test]
        public void IsCounterClockwise2D2()
        {
            Assert.IsFalse(MathHelper.IsClockwise(new Vector2(10, 20), new Vector2(0, 20), new Vector2(10, 30)), "triangle is clockwise");
        }


        [Test]
        public void IsClockwise3D()
        {
            Assert.IsTrue(MathHelper.IsClockwise(new Vector2(0, 0), new Vector2(0, 20), new Vector2(10, 10), Vector3.BackwardLH), "triangle is counter clockwise");
        }

        [Test]
        public void IsClockwise3D2()
        {
            Assert.IsTrue(MathHelper.IsClockwise(new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, -10), Vector3.BackwardLH), "triangle is counter clockwise");
        }


        [Test]
        public void IsCounterClockwise3D()
        {
            Assert.IsFalse(MathHelper.IsClockwise(new Vector2(10, 20), new Vector2(10, 30), new Vector2(0, 20), Vector3.BackwardLH), "triangle is clockwise");
        }

        [Test]
        public void IsCounterClockwise3D2()
        {
            Assert.IsFalse(MathHelper.IsClockwise(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 20), Vector3.BackwardLH), "triangle is clockwise");
        }

        [Test]
        public void NearEquals()
        {
            double a = 0.99;
            double b = 0;
            Assert.IsFalse(MathHelper.NearEqual(a, b));
        }
    }
}
