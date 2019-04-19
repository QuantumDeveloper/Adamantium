using System;

namespace Adamantium.Mathematics
{
    public static class Collision2D
    {
        public static bool IsPointOnSegment(ref LineSegment segment, ref Vector3D point)
        {
            var v1 = segment.Direction;
            var v2 = point - segment.Start;
            var v3 = point - segment.End;

            //Find cross product for (v3-v1)-(v2-v1) to tell if points v1,v2 and v3 are aligned.
            //If it is so, then value should be near zero (less than Epsilon)
            //This additional calculation gives more precision and exclude false positive results
            //var cross = (v3.Y - v1.Y) * (v2.X - v1.X) - (v3.X - v1.X) * (v2.Y - v1.Y);
            //if (MathHelper.IsZero(cross))
            //    return false;

            v1 = segment.DirectionNormalized;
            v2 = Vector3D.Normalize(point - segment.Start);
            v3 = point - segment.End;

            var v2v1 = Vector3F.Dot(v2, v1) - 1.0f;
            if (MathHelper.NearEqual(v2v1, Polygon.Epsilon) && Vector3D.Dot(v3, v1) < 0)
            {
                return true;
            }

            //Second approach
            var ab = segment.Direction.Length();
            var ap = (point - segment.Start).Length();
            var bp = (point - segment.End).Length();
            if (MathHelper.WithinEpsilon(ab, ap + bp, Polygon.Epsilon))
            {
                return true;
            }

            return false;
        }

        public static bool SegmentSegmentIntersection(ref LineSegment segment1, ref LineSegment segment2, out Vector3D point)
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
                point = Vector3D.Zero;
                return false;
            }

            var tNumerator = MathHelper.Determinant(q - p, s);
            var uNumerator = MathHelper.Determinant(q - p, r);
            var t = tNumerator / denominator;
            var u = uNumerator / denominator;

            //if (MathHelper.NearEqual(t, 0) && t < 0)
            //{
            //    t = 0;
            //}

            //if (MathHelper.NearEqual(t, 1) && t > 1)
            //{
            //    t = 1;
            //}

            //if (MathHelper.NearEqual(u, 0) && u < 0)
            //{
            //    u = 0;
            //}

            //if (MathHelper.NearEqual(u, 1) && u > 1)
            //{
            //    u = 1;
            //}

            if (t < 0 || t > 1 || u < 0 || u > 1)
            {
                point = Vector3D.Zero;
                return false;
            }

            point = p + r * t;
            return true;
        }

        public static bool RaySegmentIntersection(ref Ray2D ray, ref Vector3D start, ref Vector3D end, out Vector3D point)
        {
            var segment = new LineSegment(start, end);
            return RaySegmentIntersection(ref ray, ref segment, out point);
        }

        public static bool RaySegmentIntersection(ref Ray2D ray, ref LineSegment segment, out Vector3D point)
        {
            var collinear = MathHelper.IsCollinear((Vector3D)ray.Direction, segment.Direction);
            if (collinear)
            {
                point = Vector3D.Zero;
                return false;
            }

            var p = (Vector3D) ray.Origin;
            var r = (Vector3D) ray.Direction;
            var q = segment.Start;
            var s = segment.Direction;
            // t = (q − p) × s / (r × s)
            // u = (q − p) × r / (r × s)

            var denominator = MathHelper.Determinant(r, s);

            if (denominator == 0.0f)
            {
                point = Vector3D.Zero;
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
                point = Vector3D.Zero;
                return false;
            }

            point = p + r * t;
            return true;
        }


        public static bool RaySegmentIntersection(ref Ray2D ray, ref LineSegment segment, out Vector3D point, out double distance)
        {
            bool collinear = MathHelper.IsCollinear(ray.Direction, (Vector2D)segment.DirectionNormalized);
            distance = float.PositiveInfinity;
            point = Vector3D.Zero;
            if (!collinear)
            {
                var p = (Vector3D)ray.Origin;
                var q = segment.Start;
                var r = (Vector3D)ray.Direction;
                var s = segment.Direction;
                // t = (q − p) × s / (r × s)
                // u = (q − p) × r / (r × s)

                var denominator = MathHelper.Determinant(r, s);

                if (MathHelper.IsZero(denominator))
                {
                    point = Vector3D.Zero;
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
                var a = Vector3D.Dot(segment.Start, segment.Direction);
                var b = Vector3D.Dot(point, segment.Direction);
                distance = Math.Abs(b - a);

                if (t < 0 || t > 1 || u < 0 || u > 1)
                {
                    return false;
                }
                return true;
            }
            return false;
        }
    }
}
