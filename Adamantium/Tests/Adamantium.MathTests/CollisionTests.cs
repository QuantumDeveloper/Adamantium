using System;
using System.Collections.Generic;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.MathTests
{
    public class CollisionTests
    {
        [Test]
        public void RayToLineIntersection3D()
        {
            Ray ray = new Ray(Vector3D.Zero, Vector3D.ForwardLH);
            Vector3D start = new Vector3D(-10, 0, 10);
            Vector3D end = new Vector3D(10, 1, 10);
            var distance = Collision.RayIntersectsLineSegment(ref ray, start, end, out Vector3F coordinates);

        }

        [Test]
        public void RayToLineAxisIntersection3D()
        {
            Ray ray = new Ray(new Vector3D(0, 1, 0), Vector3D.ForwardLH);
            Vector3D start = new Vector3D(0, 0.05, 0);
            Vector3D end = new Vector3D(0, 1, 0);
            var distanceUp = Collision.RayIntersectsLineSegment(ref ray, start, end, out Vector3F coordinatesUp);

            ray = new Ray(new Vector3D(1, 0, 0), Vector3D.Left);
            start = new Vector3D(0, 0, 0.05);
            end = new Vector3D(0, 0, 1);
            var distanceForward = Collision.RayIntersectsLineSegment(ref ray, start, end, out Vector3F coordinatesForward);

            ray = new Ray(new Vector3D(7.72, 0, -10), Vector3D.ForwardLH);
            start = new Vector3D(0.05, 0, 0);
            end = new Vector3D(7.78, 0, 0);
            var distanceRight = Collision.RayIntersectsLineSegment(ref ray, start, end, out Vector3F coordinatesRight);
        }


        [Test]
        public void RayToLineIntersection2D()
        {
            Vector3D start = new Vector3D(0.4999995, 0.86);
            Vector3D end = new Vector3D(0);
            Ray2D ray = new Ray2D(new Vector2D(0.5, 1), -Vector2D.UnitY);

            var segment = new LineSegment(start, end);
            Collision2D.RaySegmentIntersection(ref ray, ref segment, out Vector3D interPoint);
        }

        [Test]
        public void IsPointOnSegment()
        {
            Vector3D point = new Vector3D(0.004363479, 0, 0);

            Vector3D start = new Vector3D(0);
            Vector3D end = new Vector3D(1, 0);

            Vector3D start2 = new Vector3D(0.9998477, 0.01745023);
            Vector3D end2 = new Vector3D(0);

            LineSegment segment1 = new LineSegment(start, end);
            LineSegment segment2 = new LineSegment(start2, end2);

            LineSegment segment3 = new LineSegment(start, point);
            Vector3D interPoint;
            //var inter = Collision2D.SegmentSegmentIntersection(ref segment2, ref segment3, out interPoint);

            var refs1 = Collision2D.IsPointOnSegment(ref segment1, ref point);
            var refs2 = Collision2D.IsPointOnSegment(ref segment2, ref point);
        }

        [Test]
        public void RayIntersectsLineSegment2D()
        {
            Ray2D ray = new Ray2D(new Vector2D(0.2, -0.2), -Vector2D.UnitY);
            LineSegment segment = new LineSegment(new Vector3D(0.2, 0.2), new Vector3D(-0.2, 0.2));
            Vector3D point;
            var intersects = Collision2D.RaySegmentIntersection(ref ray, ref segment, out point);

            Assert.IsFalse(intersects);
        }


        [Test]
        public void RayIntersectsLineSegment2D_Should_Intersects()
        {
            Ray2D ray = new Ray2D(new Vector2D(-0.2, 0.2), -Vector2D.UnitY);
            LineSegment segment = new LineSegment(new Vector3D(0.2, 0.2), new Vector3D(-0.2, 0.2));
            Vector3D point;
            var intersects = Collision2D.RaySegmentIntersection(ref ray, ref segment, out point);

            Assert.IsTrue(intersects);
        }

        [Test]
        public void RayIntersectsLineSegment2D_Correfct()
        {
            Ray2D ray = new Ray2D(new Vector2D(0.2, -0.2), Vector2D.UnitY);
            LineSegment segment = new LineSegment(new Vector3D(0.2, 0.2), new Vector3D(-0.2, 0.2));
            Vector3D point;
            var intersects = Collision2D.RaySegmentIntersection(ref ray, ref segment, out point);

            Assert.IsTrue(intersects);
            Assert.AreEqual(0.2, point.X);
            Assert.AreEqual(0.2, point.Y);
        }


        [Test]
        public void LineSegmentIntersectsLineSegment2D()
        {
            LineSegment segment = new LineSegment(new Vector3D(0.2, 0.2), new Vector3D(-0.2, 0.2));
            LineSegment segment2 = new LineSegment(new Vector3D(0.2, -0.2), new Vector3D(0.2, -0.4));
            Vector3D point;
            var intersects = Collision2D.SegmentSegmentIntersection(ref segment, ref segment2, out point);

            Assert.IsFalse(intersects);
        }

        [Test]
        public void IsPointOnSegmentExtended()
        {
            var range = 360.0;
            var tessellation = 40;
            double angle = range / tessellation;

            var angleItem = MathHelper.DegreesToRadians((float)angle);
            var startAngle = MathHelper.DegreesToRadians(0);
            angle = startAngle;
            var center = new Vector3D(0, 0.0, 0);
            var radiusX = 10.2;
            var radiusY = 10.2;
            List<Vector3D> points = new List<Vector3D>();
            for (int i = 0; i <= tessellation; ++i)
            {
                var x = center.X + (radiusX * Math.Cos(angle));
                var y = center.Y + (radiusY * Math.Sin(angle));
                if (Math.Abs(x) < Polygon.Epsilon)
                {
                    x = 0;
                }
                if (Math.Abs(y) < Polygon.Epsilon)
                {
                    y = 0;
                }

                var vertex = new Vector3D(x, y, 0);

                points.Add(vertex);

                angle += angleItem;
            }

            Vector3D minimum = new Vector3D(double.MaxValue);
            Vector3D maximum = new Vector3D(double.MinValue);

            for (int i = 0; i < points.Count; ++i)
            {
                var point = points[i];
                Vector3D.Min(ref minimum, ref point, out minimum);
                Vector3D.Max(ref maximum, ref point, out maximum);
            }
            var HighestPoint = new Vector3D(minimum.X, maximum.Y, 0);
            
            var segments = Polygon.SplitOnSegments(points);

            for (var index = 0; index < points.Count-1; index++)
            {
                var point = points[index];

                Vector3D interPoint;
                var ray = new Ray2D(new Vector2D(point.X, HighestPoint.Y), -Vector2D.UnitY);
                var lineSegment = new LineSegment(point, points[index+1]);
                Collision2D.RaySegmentIntersection(ref ray, ref lineSegment, out interPoint);
                var result = Collision2D.IsPointOnSegment(ref lineSegment, ref point);

                Assert.IsTrue(result);
            }
        }
    }
}
