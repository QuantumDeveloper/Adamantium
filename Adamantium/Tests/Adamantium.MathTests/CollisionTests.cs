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
            var start = new Vector2D(0.4999995, 0.86);
            var end = new Vector2D(0);
            var ray = new Ray2D(new Vector2D(0.5, 1), -Vector2D.UnitY);

            var segment = new LineSegment2D(start, end);
            Collision2D.RaySegmentIntersection(ref ray, ref segment, out var interPoint);
        }

        [Test]
        public void IsPointOnSegment()
        {
            var point = new Vector2D(0.004363479, 0);

            var start = new Vector2D(0);
            var end = new Vector2D(1, 0);

            var start2 = new Vector2D(0.9998477, 0.01745023);
            var end2 = new Vector2D(0);

            LineSegment2D segment1 = new LineSegment2D(start, end);
            LineSegment2D segment2 = new LineSegment2D(start2, end2);

            LineSegment2D segment3 = new LineSegment2D(start, point);
            Vector2D interPoint;
            //var inter = Collision2D.SegmentSegmentIntersection(ref segment2, ref segment3, out interPoint);

            var refs1 = Collision2D.IsPointOnSegment(ref segment1, ref point);
            var refs2 = Collision2D.IsPointOnSegment(ref segment2, ref point);
        }

        [Test]
        public void RayIntersectsLineSegment2D()
        {
            Ray2D ray = new Ray2D(new Vector2D(0.2, -0.2), -Vector2D.UnitY);
            LineSegment2D segment2D = new LineSegment2D(new Vector2D(0.2, 0.2), new Vector2D(-0.2, 0.2));
            var intersects = Collision2D.RaySegmentIntersection(ref ray, ref segment2D, out var point);

            Assert.IsFalse(intersects);
        }


        [Test]
        public void RayIntersectsLineSegment2D_Should_Intersects()
        {
            Ray2D ray = new Ray2D(new Vector2D(-0.2, 0.2), -Vector2D.UnitY);
            LineSegment2D segment2D = new LineSegment2D(new Vector2D(0.2, 0.2), new Vector2D(-0.2, 0.2));
            var intersects = Collision2D.RaySegmentIntersection(ref ray, ref segment2D, out var point);

            Assert.IsTrue(intersects);
        }

        [Test]
        public void RayIntersectsLineSegment2D_Correct()
        {
            Ray2D ray = new Ray2D(new Vector2D(0.2, -0.2), Vector2D.UnitY);
            LineSegment2D segment2D = new LineSegment2D(new Vector2D(0.2, 0.2), new Vector2D(-0.2, 0.2));
            var intersects = Collision2D.RaySegmentIntersection(ref ray, ref segment2D, out var point);

            Assert.IsTrue(intersects);
            Assert.AreEqual(0.2, point.X);
            Assert.AreEqual(0.2, point.Y);
        }


        [Test]
        public void LineSegmentIntersectsLineSegment2D()
        {
            LineSegment2D segment2D = new LineSegment2D(new Vector2D(0.2, 0.2), new Vector2D(-0.2, 0.2));
            LineSegment2D segment2 = new LineSegment2D(new Vector2D(0.2, -0.2), new Vector2D(0.2, -0.4));
            var intersects = Collision2D.SegmentSegmentIntersection(ref segment2D, ref segment2, out var point);

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
            var points = new List<Vector2D>();
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

                var vertex = new Vector2D(x, y);

                points.Add(vertex);

                angle += angleItem;
            }

            var minimum = new Vector2D(double.MaxValue);
            var maximum = new Vector2D(double.MinValue);

            for (int i = 0; i < points.Count; ++i)
            {
                var point = points[i];
                Vector2D.Min(ref minimum, ref point, out minimum);
                Vector2D.Max(ref maximum, ref point, out maximum);
            }
            var highestPoint = new Vector2D(minimum.X, maximum.Y);
            
            var segments = Polygon.SplitOnSegments(points);

            for (var index = 0; index < points.Count-1; index++)
            {
                var point = points[index];

                var ray = new Ray2D(new Vector2D(point.X, highestPoint.Y), -Vector2D.UnitY);
                var lineSegment = new LineSegment2D(point, points[index+1]);
                Collision2D.RaySegmentIntersection(ref ray, ref lineSegment, out var interPoint);
                var result = Collision2D.IsPointOnSegment(ref lineSegment, ref point);

                Assert.IsTrue(result);
            }
        }
    }
}
