using System;
using System.Runtime.CompilerServices;

namespace Adamantium.Mathematics
{
    public static class Collision2D
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining|MethodImplOptions.AggressiveOptimization)]
        public static bool IsPointOnSegment(ref LineSegment2D segment2D, ref Vector2D point)
        {
            var ab = segment2D.Direction.Length();
            var ap = Vector2D.Subtract(point, segment2D.Start).Length();
            var bp = Vector2D.Subtract(point, segment2D.End).Length();
            return MathHelper.WithinEpsilon(ab, ap + bp, Polygon.Epsilon);
        }

        public static bool SegmentSegmentIntersection(ref LineSegment2D segment1, ref LineSegment2D segment2, out Vector2D point)
        {
            var p = segment1.Start;
            var q = segment2.Start;
            var r = segment1.Direction;
            var s = segment2.Direction;
            // t = (q − p) × s / (r × s)
            // u = (q − p) × r / (r × s)

            var denominator = MathHelper.Determinant(r, s);

            if (denominator == 0.0)
            {
                point = Vector2D.Zero;
                return false;
            }

            var tNumerator = MathHelper.Determinant(q - p, s);
            var uNumerator = MathHelper.Determinant(q - p, r);
            var t = tNumerator / denominator;
            var u = uNumerator / denominator;

            if (t < 0 || t > 1 || u < 0 || u > 1)
            {
                point = Vector2D.Zero;
                return false;
            }

            point = p + r * t;
            return true;
        }

        public static bool RaySegmentIntersection(ref Ray2D ray, ref Vector2D start, ref Vector2D end, out Vector2D point)
        {
            var segment = new LineSegment2D(start, end);
            return RaySegmentIntersection(ref ray, ref segment, out point);
        }

        public static bool RaySegmentIntersection(ref Ray2D ray, ref LineSegment2D segment2D, out Vector2D point)
        {
            var collinear = ray.Direction.IsCollinear(segment2D.Direction);
            if (collinear)
            {
                point = Vector2D.Zero;
                return false;
            }

            var p = ray.Origin;
            var r = ray.Direction;
            var q = segment2D.Start;
            var s = segment2D.Direction;
            // t = (q − p) × s / (r × s)
            // u = (q − p) × r / (r × s)

            var denominator = MathHelper.Determinant(r, s);

            if (denominator == 0.0f)
            {
                point = Vector2D.Zero;
                return false;
            }

            var tNumerator = MathHelper.Determinant(q - p, s);
            var uNumerator = MathHelper.Determinant(q - p, r);

            var t = tNumerator / denominator;
            var u = uNumerator / denominator;

            
            if (MathHelper.NearEqual(t, 0) && t < 0)
            {
                t = 0;
            }

            if (MathHelper.NearEqual(t, 1) && t > 1)
            {
                t = 1;
            }

            if (MathHelper.NearEqual(u, 0) && u < 0)
            {
                u = 0;
            }

            if (MathHelper.NearEqual(u, 1) && u > 1)
            {
                u = 1;
            }
            

            if (t < 0 || u < 0 || u > 1)
            {
                point = Vector2D.Zero;
                return false;
            }

            point = p + r * t;
            return true;
        }


        public static bool RaySegmentIntersection(ref Ray2D ray, ref LineSegment2D segment2D, out Vector2D point, out double distance)
        {
            var collinear = ray.Direction.IsCollinear(segment2D.Direction);
            distance = float.PositiveInfinity;
            point = Vector2D.Zero;
            if (collinear) return false;
            
            var p = ray.Origin;
            var q = segment2D.Start;
            var r = ray.Direction;
            var s = segment2D.Direction;
            // t = (q − p) × s / (r × s)
            // u = (q − p) × r / (r × s)

            var denominator = MathHelper.Determinant(r, s);

            if (MathHelper.IsZero(denominator))
            {
                point = Vector2D.Zero;
                return false;
            }

            var tNumerator = MathHelper.Determinant(q - p, s);
            var uNumerator = MathHelper.Determinant(q - p, r);

            var t = tNumerator / denominator;
            var u = uNumerator / denominator;

            point = p + r * t;
            //If ray and line not intersecting, try to find out closest distance between them
            //and if distance is less than Epsilon, then we can say that ray intersects line
            //because distance is near zero
            var a = Vector2D.Dot(segment2D.Start, segment2D.Direction);
            var b = Vector2D.Dot(point, segment2D.Direction);
            distance = Math.Abs(b - a);

            if (t < 0 || t > 1 || u < 0 || u > 1)
            {
                return false;
            }
            return true;
        }
    }
}
