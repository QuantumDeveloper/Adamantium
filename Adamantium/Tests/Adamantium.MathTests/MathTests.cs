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
            List<Vector3D> points = new List<Vector3D>();
            points.Add(new Vector3D(0, 0, 0));
            points.Add(new Vector3D(0, 20, 0));
            points.Add(new Vector3D(10, 10, 0));
            points.Add(new Vector3D(20, 20, 0));
            points.Add(new Vector3D(20, 0, 0));
            var timer = Stopwatch.StartNew();
            Assert.IsTrue(Polygon.IsConcave(points), "polygon is not concave");
            timer.Stop();
        }

        [Test]
        public void Concave2Test()
        {
            List<Vector3D> points = new List<Vector3D>();
            points.Add(new Vector3D(0, 0, 0));
            points.Add(new Vector3D(0, 20, 0));
            points.Add(new Vector3D(10, 10, 0));
            points.Add(new Vector3D(20, 20, 0));
            points.Add(new Vector3D(20, 0, 0));

            Polygon polygon = new Polygon();
            polygon.Polygons.Add(new PolygonItem(points));

            var timer = Stopwatch.StartNew();
            Assert.IsTrue(Polygon.IsConcave(polygon.MergedSegments), "polygon is not concave");
            timer.Stop();
        }

        [Test]
        public void ConvexTest()
        {
            List<Vector3D> points = new List<Vector3D>();
            points.Add(new Vector3D(0, 0, 0));
            points.Add(new Vector3D(0, 20, 0));
            points.Add(new Vector3D(10, 20, 0));
            points.Add(new Vector3D(20, 20, 0));
            points.Add(new Vector3D(20, 0, 0));
            points.Add(new Vector3D(0, 0, 0));
            var timer = Stopwatch.StartNew();
            Assert.IsFalse(Polygon.IsConcave(points), "polygon is not convex");
            timer.Stop();

        }

        [Test]
        public void PolygonHasNoSelfIntersections()
        {
            List<Vector3D> points = new List<Vector3D>();
            points.Add(new Vector3D(0, 0, 0));
            points.Add(new Vector3D(0, 20, 0));
            points.Add(new Vector3D(10, 20, 0));
            points.Add(new Vector3D(20, 20, 0));
            points.Add(new Vector3D(20, 0, 0));
            points.Add(new Vector3D(0, 0, 0));
            PolygonItem p = new PolygonItem(points);
            p.SplitOnSegments();
            var hasSelfIntersections = p.CheckForSelfIntersection(FillRule.NonZero);
            Assert.IsTrue(!hasSelfIntersections, "polygon has selfintersections");
        }

        [Test]
        public void PolygonHasSelfIntersections()
        {
            List<Vector3D> points = new List<Vector3D>();
            points.Add(new Vector3D(-10, 0, 0));
            points.Add(new Vector3D(0, 30, 0));
            points.Add(new Vector3D(10, 0, 0));
            points.Add(new Vector3D(-15, 15, 0));
            points.Add(new Vector3D(15, 15, 0));
            PolygonItem p = new PolygonItem(points);
            p.SplitOnSegments();
            var hasSelfIntersections = p.CheckForSelfIntersection(FillRule.NonZero);
            Assert.IsTrue(hasSelfIntersections, "polygon has no selfintersections");
            Assert.AreEqual(p.SelfIntersectedPoints.Count, 5);
        }

        [Test]
        public void IsPointOnLineTest()
        {
            var segment = new LineSegment(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0));
            var point = new Vector3D(5, 0, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsTrue(isOnLine, "point is not on the line");
        }

        [Test]
        public void IsPointOnLeftSegmentEdgeTest()
        {
            var segment = new LineSegment(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0));
            var point = new Vector3D(0, 0, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsTrue(isOnLine, "point is not on the line");
        }

        [Test]
        public void IsPointOnRightSegmentEdgeTest()
        {
            var segment = new LineSegment(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0));
            var point = new Vector3D(10, 0, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsTrue(isOnLine, "point is not on the line");
        }

        [Test]
        public void IsPointNotOnSegmentTest2()
        {
            var segment = new LineSegment(new Vector3D(0, 0, 0), new Vector3D(10, 10, 0));
            var point = new Vector3D(5, 5.01f, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsFalse(isOnLine, "point is on the line");
        }

        [Test]
        public void IsPointNotOnLineTest()
        {
            var segment = new LineSegment(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0));
            var point = new Vector3D(11, 0, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsFalse(isOnLine, "point is on the line");
        }

        [Test]
        public void IsPointNotOnSegmentTest()
        {
            var segment = new LineSegment(new Vector3D(0, 0, 0), new Vector3D(0, 20, 0));
            var point = new Vector3D(5, 0, 0);
            var isOnLine = Collision2D.IsPointOnSegment(ref segment, ref point);
            Assert.IsFalse(isOnLine, "point is on the line");
        }

        [Test]
        public void SegmentSegmentIntersectionTest()
        {
            Vector3D intersectionPoint;
            var segment1 = new LineSegment(new Vector3D(-10, 0, 0), new Vector3D(0, 30, 0));
            var segment2 = new LineSegment(new Vector3D(-15, 15, 0), new Vector3D(15, 15, 0));

            var isLineIntersects = Collision2D.SegmentSegmentIntersection(ref segment1, ref segment2, out intersectionPoint);
            Assert.IsTrue(isLineIntersects, "lines is not intersecting");
            Assert.AreEqual(new Vector3D(-5, 15, 0), intersectionPoint);
        }


        [Test]
        public void RayLineIntersectionTest()
        {
            Vector3D intersectionPoint;
            LineSegment s = new LineSegment(new Vector3D(1, 1, 0), new Vector3D(10, 1, 0));
            var ray = new Ray2D(new Vector2D(2, 10), new Vector2D(0, -1));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out intersectionPoint);
            Assert.IsTrue(isLineIntersects, "lines is not intersecting");
            Assert.AreEqual(new Vector3D(2, 1, 0), intersectionPoint);
        }

        [Test]
        public void RayLineIntersectionTest2()
        {
            Vector3D intersectionPoint;
            LineSegment s = new LineSegment(new Vector3D(-20, 8, 0), new Vector3D(10, 1, 0));
            var ray = new Ray2D(new Vector2D(-2, 10), new Vector2D(0, -1));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out intersectionPoint);
            Assert.IsTrue(isLineIntersects, "ray and line is not intersecting");
            Assert.AreEqual(new Vector3D(-2, 3.8f, 0), intersectionPoint);
        }

        [Test]
        public void CollinearRayLineIntersectionTest()
        {
            Vector3D intersectionPoint;
            LineSegment s = new LineSegment(new Vector3D(20, 0, 0), new Vector3D(30, 0, 0));
            var ray = new Ray2D(new Vector2D(1, 0), new Vector2D(1, 0));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out intersectionPoint);
            Assert.IsFalse(isLineIntersects, "ray and line is not collinear");
        }

        [Test]
        public void CollinearRayLineIntersectionTest2()
        {
            Vector3D intersectionPoint;
            LineSegment s = new LineSegment(new Vector3D(20, 0, 0), new Vector3D(10, 0, 0));
            var ray = new Ray2D(new Vector2D(1, 0), new Vector2D(1, 0));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out intersectionPoint);
            Assert.IsFalse(isLineIntersects, "ray and line is not collinear");
        }

        [Test]
        public void RayWithStartOfLineIntersection()
        {
            Vector3D intersectionPoint;
            LineSegment s = new LineSegment(new Vector3D(20, 0, 0), new Vector3D(30, 0, 0));
            var ray = new Ray2D(new Vector2D(20, 10), new Vector2D(0, -1));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out intersectionPoint);
            Assert.IsTrue(isLineIntersects, "ray and line is not intersecting");
            Assert.AreEqual(new Vector3D(20, 0, 0), intersectionPoint);
        }

        [Test]
        public void RayWithEndOfLineIntersection()
        {
            Vector3D intersectionPoint;
            LineSegment s = new LineSegment(new Vector3D(20, 0, 0), new Vector3D(30, 0, 0));
            var ray = new Ray2D(new Vector2D(30, 10), new Vector2D(0, -1));
            var isLineIntersects = Collision2D.RaySegmentIntersection(ref ray, ref s, out intersectionPoint);
            Assert.IsTrue(isLineIntersects, "ray and line is not intersecting");
            Assert.AreEqual(new Vector3D(30, 0, 0), intersectionPoint);
        }


        [Test]
        public void TriangulationTest()
        {
            List<Vector3D> points = new List<Vector3D>();
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

            points.Add(new Vector3D(-10, 0, 0));
            points.Add(new Vector3D(0, 30, 0));
            points.Add(new Vector3D(10, 0, 0));
            points.Add(new Vector3D(-15, 15, 0));
            points.Add(new Vector3D(15, 15, 0));

            points.Add(new Vector3D(-10, 0, 0));
            points.Add(new Vector3D(0, -30, 0));
            points.Add(new Vector3D(10, 0, 0));
            points.Add(new Vector3D(-15, -15, 0));
            points.Add(new Vector3D(15, -15, 0));

            polygon.Polygons.Add(new PolygonItem(points));

            polygon.FillRule = FillRule.NonZero;
            var result = polygon.Fill();

        }

        [Test]
        public void IsClockwise2D()
        {
            Assert.IsTrue(MathHelper.IsClockwise(new Vector2D(10, 0), new Vector2D(10, 20), new Vector2D(0, 20)), "triangle is counter clockwise");
        }


        [Test]
        public void IsCounterClockwise2D()
        {
            Assert.IsFalse(MathHelper.IsClockwise(new Vector2D(0, 20), new Vector2D(10, 30), new Vector2D(10, 20)), "triangle is clockwise");
        }

        [Test]
        public void IsCounterClockwise2D2()
        {
            Assert.IsFalse(MathHelper.IsClockwise(new Vector2D(10, 20), new Vector2D(0, 20), new Vector2D(10, 30)), "triangle is clockwise");
        }


        [Test]
        public void IsClockwise3D()
        {
            Assert.IsTrue(MathHelper.IsClockwise(new Vector3D(0, 0, 0), new Vector3D(0, 20, 0), new Vector3D(10, 10, 0), Vector3D.BackwardLH), "triangle is counter clockwise");
        }

        [Test]
        public void IsClockwise3D2()
        {
            Assert.IsTrue(MathHelper.IsClockwise(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0), new Vector3D(10, -10, 0), Vector3D.BackwardLH), "triangle is counter clockwise");
        }


        [Test]
        public void IsCounterClockwise3D()
        {
            Assert.IsFalse(MathHelper.IsClockwise(new Vector3D(10, 20, 0), new Vector3D(10, 30, 0), new Vector3D(0, 20, 0), Vector3D.BackwardLH), "triangle is clockwise");
        }

        [Test]
        public void IsCounterClockwise3D2()
        {
            Assert.IsFalse(MathHelper.IsClockwise(new Vector3D(0, 0, 0), new Vector3D(10, 0, 0), new Vector3D(0, 20, 0), Vector3D.BackwardLH), "triangle is clockwise");
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