using System;
using System.Collections.Generic;

namespace Adamantium.Mathematics
{
    /// <summary>
    /// Bounding volume using an oriented bounding box.
    /// </summary>
    public struct OrientedBoundingBox : IEquatable<OrientedBoundingBox>
    {
        #region Constants
        public const int CornerCount = 8;

        // Epsilon value used in ray tests, where a ray might hit the box almost edge-on.
        const float RAY_EPSILON = 1e-20F;
        #endregion

        #region Fields
        public Vector3F Center;
        public Vector3F HalfExtent;
        public QuaternionF Orientation;
        #endregion

        /// <summary>
        /// Full OrientedBoundingBox size
        /// </summary>
        public Vector3F Size => HalfExtent * 2;

        public Vector3F Minimum;

        public Vector3F Maximum;

        #region Constructors

        /// <summary>
        /// Create an oriented box with the given center, half-extents, and orientation.
        /// </summary>
        public OrientedBoundingBox(Vector3F center, Vector3F halfExtents, QuaternionF orientation)
        {
            Center = center;
            HalfExtent = halfExtents;
            Orientation = orientation;
            Minimum = center - halfExtents;
            Maximum = center + halfExtents;
        }

        public OrientedBoundingBox(Vector3F minimum, Vector3F maximum)
        {
            Center = minimum + (maximum - minimum) / 2f;
            HalfExtent = maximum - Center;
            Orientation = QuaternionF.Identity;
            Minimum = minimum;
            Maximum = maximum;
        }

        public static OrientedBoundingBox FromPoints(Vector3F[] points)
        {
            if (points == null || points.Length == 0)
                throw new ArgumentNullException(nameof(points));

            Vector3F minimum = new Vector3F(float.MaxValue);
            Vector3F maximum = new Vector3F(float.MinValue);

            for (int i = 0; i < points.Length; ++i)
            {
                Vector3F.Min(ref minimum, ref points[i], out minimum);
                Vector3F.Max(ref maximum, ref points[i], out maximum);
            }

            var center = minimum + (maximum - minimum) / 2f;
            var halfExtent = maximum - center;
            return new OrientedBoundingBox(center, halfExtent, QuaternionF.Identity);
        }

        public static OrientedBoundingBox FromPoints(Vector3[] points)
        {
            if (points == null || points.Length == 0)
                throw new ArgumentNullException(nameof(points));

            Vector3 minimum = new Vector3(float.MaxValue);
            Vector3 maximum = new Vector3(float.MinValue);

            for (int i = 0; i < points.Length; ++i)
            {
                Vector3.Min(ref minimum, ref points[i], out minimum);
                Vector3.Max(ref maximum, ref points[i], out maximum);
            }

            var center = minimum + (maximum - minimum) / 2f;
            var halfExtent = maximum - center;
            return new OrientedBoundingBox(center, halfExtent, QuaternionF.Identity);
        }

        public static OrientedBoundingBox FromPoints(List<Vector3F> points)
        {
            if (points == null || points.Count == 0)
                throw new ArgumentNullException(nameof(points));

            Vector3F minimum = new Vector3F(float.MaxValue);
            Vector3F maximum = new Vector3F(float.MinValue);

            for (int i = 0; i < points.Count; ++i)
            {
                minimum = Vector3F.Min(minimum, points[i]);
                maximum = Vector3F.Max(maximum, points[i]);
            }

            var center = minimum + (maximum - minimum) / 2f;
            var halfExtent = maximum - center;
            return new OrientedBoundingBox(center, halfExtent, QuaternionF.Identity);
        }

        /// <summary>
        /// Create an oriented box from an axis-aligned box.
        /// </summary>
        public static OrientedBoundingBox FromBoundingBox(BoundingBox box)
        {
            Vector3F mid = (box.Minimum + box.Maximum) * 0.5f;
            Vector3F halfExtent = (box.Maximum - box.Minimum) * 0.5f;
            return new OrientedBoundingBox(mid, halfExtent, QuaternionF.Identity);
        }

        /// <summary>
        /// Transform the given bounding box by a rotation around the origin followed by a translation 
        /// </summary>
        /// <param name="rotation"></param>
        /// <param name="translation"></param>
        /// <returns>A new bounding box, transformed relative to this one</returns>
        public OrientedBoundingBox Transform(QuaternionF rotation, Vector3F translation)
        {
            return new OrientedBoundingBox(Vector3F.Transform(Center, rotation) + translation,
                                            HalfExtent,
                                            Orientation * rotation);
        }

        /// <summary>
        /// Transform the given bounding box by a uniform scale and rotation around the origin followed
        /// by a translation
        /// </summary>
        /// <returns>A new bounding box, transformed relative to this one</returns>
        public OrientedBoundingBox Transform(float scale, QuaternionF rotation, Vector3F translation)
        {
            return new OrientedBoundingBox(Vector3F.Transform(Center * scale, rotation) + translation,
                                            HalfExtent * scale,
                                            Orientation * rotation);
        }

        /// <summary>
        /// Transform the given bounding box by a non-unifrom scale and rotation around the origin followed
        /// by a translation
        /// </summary>
        /// <returns>A new bounding box, transformed relative to this one</returns>
        public OrientedBoundingBox Transform(Vector3F scale, QuaternionF rotation, Vector3F translation)
        {
            return new OrientedBoundingBox(Vector3F.Transform(Center * scale, rotation) + translation,
                                            HalfExtent * scale,
                                            Orientation * rotation);
        }

        #endregion

        #region IEquatable implementation

        public bool Equals(OrientedBoundingBox other)
        {
            return (Center == other.Center && HalfExtent == other.HalfExtent && Orientation == other.Orientation);
        }

        public override bool Equals(Object obj)
        {
            if (obj != null && obj is OrientedBoundingBox)
            {
                OrientedBoundingBox other = (OrientedBoundingBox)obj;
                return (Center == other.Center && HalfExtent == other.HalfExtent && Orientation == other.Orientation);
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return Center.GetHashCode() ^ HalfExtent.GetHashCode() ^ Orientation.GetHashCode();
        }

        public static bool operator ==(OrientedBoundingBox a, OrientedBoundingBox b)
        {
            return Equals(a, b);
        }

        public static bool operator !=(OrientedBoundingBox a, OrientedBoundingBox b)
        {
            return !Equals(a, b);
        }

        public override string ToString()
        {
            return "{Center:" + Center.ToString() +
                   " Extents:" + HalfExtent.ToString() +
                   " Orientation:" + Orientation.ToString() + "}";
        }

        #endregion

        #region Test vs. BoundingBox

        /// <summary>
        /// Determine if box A intersects box B.
        /// </summary>
        public bool Intersects(ref BoundingBox box)
        {
            Vector3F boxCenter = (box.Maximum + box.Minimum) * 0.5f;
            Vector3F boxHalfExtent = (box.Maximum - box.Minimum) * 0.5f;

            Matrix4x4F mb = Matrix4x4F.RotationQuaternion(Orientation);
            mb.TranslationVector = Center - boxCenter;

            return ContainsRelativeBox(ref boxHalfExtent, ref HalfExtent, ref mb) != ContainmentType.Disjoint;
        }

        /// <summary>
        /// Determine if this box contains, intersects, or is disjoint from the given BoundingBox.
        /// </summary>
        public ContainmentType Contains(ref BoundingBox box)
        {
            Vector3F boxCenter = (box.Maximum + box.Minimum) * 0.5f;
            Vector3F boxHalfExtent = (box.Maximum - box.Minimum) * 0.5f;

            // Build the 3x3 rotation matrix that defines the orientation of 'other' relative to this box
            QuaternionF relOrient;
            QuaternionF.Conjugate(ref Orientation, out relOrient);

            Matrix4x4F relTransform = Matrix4x4F.RotationQuaternion(relOrient);
            relTransform.TranslationVector = Vector3F.TransformNormal(boxCenter - Center, relTransform);

            return ContainsRelativeBox(ref HalfExtent, ref boxHalfExtent, ref relTransform);
        }

        /// <summary>
        /// Determine if box A contains, intersects, or is disjoint from box B.
        /// </summary>
        public static ContainmentType Contains(ref BoundingBox boxA, ref OrientedBoundingBox oboxB)
        {
            Vector3F boxA_halfExtent = (boxA.Maximum - boxA.Minimum) * 0.5f;
            Vector3F boxA_center = (boxA.Maximum + boxA.Minimum) * 0.5f;
            Matrix4x4F mb = Matrix4x4F.RotationQuaternion(oboxB.Orientation);
            mb.TranslationVector = oboxB.Center - boxA_center;

            return ContainsRelativeBox(ref boxA_halfExtent, ref oboxB.HalfExtent, ref mb);
        }

        #endregion

        #region Test vs. OrientedBoundingBox

        /// <summary>
        /// Returns true if this box intersects the given other box.
        /// </summary>
        public bool Intersects(ref OrientedBoundingBox other)
        {
            return Contains(ref other) != ContainmentType.Disjoint;
        }

        /// <summary>
        /// Determine whether this box contains, intersects, or is disjoint from
        /// the given other box.
        /// </summary>
        public ContainmentType Contains(ref OrientedBoundingBox other)
        {
            // Build the 3x3 rotation matrix that defines the orientation of 'other' relative to this box
            QuaternionF invOrient;
            QuaternionF.Conjugate(ref Orientation, out invOrient);
            QuaternionF relOrient;
            QuaternionF.Multiply(ref invOrient, ref other.Orientation, out relOrient);

            Matrix4x4F relTransform = Matrix4x4F.RotationQuaternion(relOrient);
            relTransform.TranslationVector = Vector3F.Transform(other.Center - Center, invOrient);

            return ContainsRelativeBox(ref HalfExtent, ref other.HalfExtent, ref relTransform);
        }

        #endregion

        #region Test vs. BoundingFrustum

        /// <summary>
        /// Determine whether this box contains, intersects, or is disjoint from
        /// the given frustum.
        /// </summary>
        public ContainmentType Contains(BoundingFrustum frustum)
        {
            // Convert this bounding box to an equivalent BoundingFrustum, so we can rely on BoundingFrustum's
            // implementation. Note that this is very slow, since BoundingFrustum builds various data structures
            // for this test that it caches internally. To speed it up, you could convert the box to a frustum
            // just once and re-use that frustum for repeated tests.
            BoundingFrustum temp = ConvertToFrustum();
            //return temp.Contains(frustum);
            return ContainmentType.Contains;
        }

        /// <summary>
        /// Returns true if this box intersects the given frustum.
        /// </summary>
        public bool Intersects(BoundingFrustum frustum)
        {
            return (Contains(frustum) != ContainmentType.Disjoint);
        }

        /// <summary>
        /// Determine whether the given frustum contains, intersects, or is disjoint from
        /// the given oriented box.
        /// </summary>
        public static ContainmentType Contains(BoundingFrustum frustum, ref OrientedBoundingBox obb)
        {
            return frustum.Contains(obb.ConvertToFrustum());
        }

        #endregion

        #region Test vs Line

        /// <summary>
        /// Check the intersection between an <see cref="OrientedBoundingBox"/> and a line defined by two points
        /// </summary>
        /// <param name="L1">The first point in the line.</param>
        /// <param name="L2">The second point in the line.</param>
        /// <returns>The type of containment the two objects have.</returns>
        /// <remarks>
        /// For accuracy, The transformation matrix for the <see cref="OrientedBoundingBox"/> must not have any scaling applied to it.
        /// Anyway, scaling using Scale method will keep this method accurate.
        /// </remarks>
        public ContainmentType ContainsLine(ref Vector3F L1, ref Vector3F L2)
        {
            var cornersCheck = Contains(new Vector3F[] { L1, L2 });
            if (cornersCheck != ContainmentType.Disjoint)
                return cornersCheck;

            //http://www.3dkingdoms.com/weekly/bbox.cpp
            // Put line in box space
            var transformation = Matrix4x4F.RotationQuaternion(Orientation) * Matrix4x4F.Translation(Center);
            Matrix4x4F invTrans;
            Matrix4x4F.Invert(ref transformation, out invTrans);

            Vector3F LB1;
            Vector3F.TransformCoordinate(ref L1, ref invTrans, out LB1);
            Vector3F LB2;
            Vector3F.TransformCoordinate(ref L1, ref invTrans, out LB2);

            // Get line midpoint and extent
            var LMid = (LB1 + LB2) * 0.5f;
            var L = (LB1 - LMid);
            var LExt = new Vector3F(Math.Abs(L.X), Math.Abs(L.Y), Math.Abs(L.Z));

            // Use Separating Axis Test
            // Separation vector from box center to line center is LMid, since the line is in box space
            if (Math.Abs(LMid.X) > HalfExtent.X + LExt.X) return ContainmentType.Disjoint;
            if (Math.Abs(LMid.Y) > HalfExtent.Y + LExt.Y) return ContainmentType.Disjoint;
            if (Math.Abs(LMid.Z) > HalfExtent.Z + LExt.Z) return ContainmentType.Disjoint;
            // Cross products of line and each axis
            if (Math.Abs(LMid.Y * L.Z - LMid.Z * L.Y) > (HalfExtent.Y * LExt.Z + HalfExtent.Z * LExt.Y)) return ContainmentType.Disjoint;
            if (Math.Abs(LMid.X * L.Z - LMid.Z * L.X) > (HalfExtent.X * LExt.Z + HalfExtent.Z * LExt.X)) return ContainmentType.Disjoint;
            if (Math.Abs(LMid.X * L.Y - LMid.Y * L.X) > (HalfExtent.X * LExt.Y + HalfExtent.Y * LExt.X)) return ContainmentType.Disjoint;
            // No separating axis, the line intersects
            return ContainmentType.Intersects;
        }

        /// <summary>
        /// Determines whether a <see cref="OrientedBoundingBox"/> contains an array of points>.
        /// </summary>
        /// <param name="points">The points array to test.</param>
        /// <returns>The type of containment.</returns>
        public ContainmentType Contains(Vector3F[] points)
        {
            var transformation = Matrix4x4F.RotationQuaternion(Orientation) * Matrix4x4F.Translation(Center);
            Matrix4x4F invTrans;
            Matrix4x4F.Invert(ref transformation, out invTrans);

            var containsAll = true;
            var containsAny = false;

            for (int i = 0; i < points.Length; i++)
            {
                Vector3F locPoint;
                Vector3F.TransformCoordinate(ref points[i], ref invTrans, out locPoint);

                locPoint.X = Math.Abs(locPoint.X);
                locPoint.Y = Math.Abs(locPoint.Y);
                locPoint.Z = Math.Abs(locPoint.Z);

                //Simple axes-aligned BB check
                if (MathHelper.NearEqual(locPoint.X, HalfExtent.X) &&
                    MathHelper.NearEqual(locPoint.Y, HalfExtent.Y) &&
                    MathHelper.NearEqual(locPoint.Z, HalfExtent.Z))
                    containsAny = true;
                if (locPoint.X < HalfExtent.X && locPoint.Y < HalfExtent.Y && locPoint.Z < HalfExtent.Z)
                    containsAny = true;
                else
                    containsAll = false;
            }

            if (containsAll)
                return ContainmentType.Contains;
            else if (containsAny)
                return ContainmentType.Intersects;
            else
                return ContainmentType.Disjoint;
        }

        #endregion

        #region Test vs. BoundingSphere

        /// <summary>
        /// Test whether this box contains, intersects, or is disjoint from the given sphere
        /// </summary>
        public ContainmentType Contains(ref BoundingSphere sphere)
        {
            // Transform the sphere into local box space
            QuaternionF iq = QuaternionF.Conjugate(Orientation);
            Vector3F localCenter = Vector3F.Transform(sphere.Center - Center, iq);

            // (dx,dy,dz) = signed distance of center of sphere from edge of box
            float dx = Math.Abs(localCenter.X) - HalfExtent.X;
            float dy = Math.Abs(localCenter.Y) - HalfExtent.Y;
            float dz = Math.Abs(localCenter.Z) - HalfExtent.Z;

            // Check for sphere completely inside box
            float r = sphere.Radius;
            if (dx <= -r && dy <= -r && dz <= -r)
                return ContainmentType.Contains;

            // Compute how far away the sphere is in each dimension
            dx = Math.Max(dx, 0.0f);
            dy = Math.Max(dy, 0.0f);
            dz = Math.Max(dz, 0.0f);

            if (dx * dx + dy * dy + dz * dz >= r * r)
                return ContainmentType.Disjoint;

            return ContainmentType.Intersects;
        }

        /// <summary>
        /// Test whether this box intersects the given sphere
        /// </summary>
        public bool Intersects(ref BoundingSphere sphere)
        {
            // Transform the sphere into local box space
            QuaternionF iq = QuaternionF.Conjugate(Orientation);
            Vector3F localCenter = Vector3F.Transform(sphere.Center - Center, iq);

            // (dx,dy,dz) = signed distance of center of sphere from edge of box
            float dx = Math.Abs(localCenter.X) - HalfExtent.X;
            float dy = Math.Abs(localCenter.Y) - HalfExtent.Y;
            float dz = Math.Abs(localCenter.Z) - HalfExtent.Z;

            // Compute how far away the sphere is in each dimension
            dx = Math.Max(dx, 0.0f);
            dy = Math.Max(dy, 0.0f);
            dz = Math.Max(dz, 0.0f);
            float r = sphere.Radius;

            return dx * dx + dy * dy + dz * dz < r * r;
        }

        /// <summary>
        /// Test whether a BoundingSphere contains, intersects, or is disjoint from a OrientedBoundingBox
        /// </summary>
        public static ContainmentType Contains(ref BoundingSphere sphere, ref OrientedBoundingBox box)
        {
            // Transform the sphere into local box space
            QuaternionF iq = QuaternionF.Conjugate(box.Orientation);
            Vector3F localCenter = Vector3F.Transform(sphere.Center - box.Center, iq);
            localCenter.X = Math.Abs(localCenter.X);
            localCenter.Y = Math.Abs(localCenter.Y);
            localCenter.Z = Math.Abs(localCenter.Z);

            // Check for box completely inside sphere
            float rSquared = sphere.Radius * sphere.Radius;
            if ((localCenter + box.HalfExtent).LengthSquared() <= rSquared)
                return ContainmentType.Contains;

            // (dx,dy,dz) = signed distance of center of sphere from edge of box
            Vector3F d = localCenter - box.HalfExtent;

            // Compute how far away the sphere is in each dimension
            d.X = Math.Max(d.X, 0.0f);
            d.Y = Math.Max(d.Y, 0.0f);
            d.Z = Math.Max(d.Z, 0.0f);

            if (d.LengthSquared() >= rSquared)
                return ContainmentType.Disjoint;

            return ContainmentType.Intersects;
        }

        #endregion

        #region Test vs. 0/1/2d primitives

        /// <summary>
        /// Returns true if this box contains the given point.
        /// </summary>
        public bool Contains(ref Vector3F point)
        {
            // Transform the point into box-local space and check against
            // our extents.
            QuaternionF qinv = QuaternionF.Conjugate(Orientation);
            Vector3F plocal = Vector3F.Transform(point - Center, qinv);

            return Math.Abs(plocal.X) <= HalfExtent.X &&
                   Math.Abs(plocal.Y) <= HalfExtent.Y &&
                   Math.Abs(plocal.Z) <= HalfExtent.Z;
        }

        /// <summary>
        /// Determine whether the given ray intersects this box. If so, returns
        /// the parametric value of the point of first intersection; otherwise
        /// returns null.
        /// </summary>
        public float? Intersects(ref Ray ray)
        {
            Matrix4x4F R = Matrix4x4F.RotationQuaternion(Orientation);

            Vector3F TOrigin = Center - ray.Position;

            float t_min = -float.MaxValue;
            float t_max = float.MaxValue;

            // X-case
            float axisDotOrigin = Vector3F.Dot(R.Right, TOrigin);
            float axisDotDir = Vector3F.Dot(R.Right, ray.Direction);

            if (axisDotDir >= -RAY_EPSILON && axisDotDir <= RAY_EPSILON)
            {
                if ((-axisDotOrigin - HalfExtent.X) > 0.0 || (-axisDotOrigin + HalfExtent.X) > 0.0f)
                    return null;
            }
            else
            {
                float t1 = (axisDotOrigin - HalfExtent.X) / axisDotDir;
                float t2 = (axisDotOrigin + HalfExtent.X) / axisDotDir;

                if (t1 > t2)
                {
                    float temp = t1;
                    t1 = t2;
                    t2 = temp;
                }

                if (t1 > t_min)
                    t_min = t1;

                if (t2 < t_max)
                    t_max = t2;

                if (t_max < 0.0f || t_min > t_max)
                    return null;
            }

            // Y-case
            axisDotOrigin = Vector3F.Dot(R.Up, TOrigin);
            axisDotDir = Vector3F.Dot(R.Up, ray.Direction);

            if (axisDotDir >= -RAY_EPSILON && axisDotDir <= RAY_EPSILON)
            {
                if ((-axisDotOrigin - HalfExtent.Y) > 0.0 || (-axisDotOrigin + HalfExtent.Y) > 0.0f)
                    return null;
            }
            else
            {
                float t1 = (axisDotOrigin - HalfExtent.Y) / axisDotDir;
                float t2 = (axisDotOrigin + HalfExtent.Y) / axisDotDir;

                if (t1 > t2)
                {
                    float temp = t1;
                    t1 = t2;
                    t2 = temp;
                }

                if (t1 > t_min)
                    t_min = t1;

                if (t2 < t_max)
                    t_max = t2;

                if (t_max < 0.0f || t_min > t_max)
                    return null;
            }

            // Z-case
            axisDotOrigin = Vector3F.Dot(R.Forward, TOrigin);
            axisDotDir = Vector3F.Dot(R.Forward, ray.Direction);

            if (axisDotDir >= -RAY_EPSILON && axisDotDir <= RAY_EPSILON)
            {
                if ((-axisDotOrigin - HalfExtent.Z) > 0.0 || (-axisDotOrigin + HalfExtent.Z) > 0.0f)
                    return null;
            }
            else
            {
                float t1 = (axisDotOrigin - HalfExtent.Z) / axisDotDir;
                float t2 = (axisDotOrigin + HalfExtent.Z) / axisDotDir;

                if (t1 > t2)
                {
                    float temp = t1;
                    t1 = t2;
                    t2 = temp;
                }

                if (t1 > t_min)
                    t_min = t1;

                if (t2 < t_max)
                    t_max = t2;

                if (t_max < 0.0f || t_min > t_max)
                    return null;
            }

            return t_min;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="point"></param>
        /// <returns></returns>
        /// <remarks>Have quite good precision</remarks>
        public bool Intersects(ref Ray ray, out Vector3F point)
        {
            // Put ray in box space
            Matrix4x4F invTrans;
            Matrix4x4F rot = Matrix4x4F.RotationQuaternion(Orientation) * Matrix4x4F.Translation(Center);
            Matrix4x4F.Invert(ref rot, out invTrans);

            Ray bRay;
            Vector3F.TransformNormal(ref ray.Direction, ref invTrans, out bRay.Direction);
            Vector3F.TransformCoordinate(ref ray.Position, ref invTrans, out bRay.Position);

            //Perform a regular ray to BoundingBox check
            var bb = new BoundingBox(-HalfExtent, HalfExtent);
            var intersects = Collision.RayIntersectsBox(ref bRay, ref bb, out point);

            //Put the result intersection back to world
            if (intersects)
                Vector3F.TransformCoordinate(ref point, ref rot, out point);

            return intersects;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="ray"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        /// <remarks>Have quite good precision</remarks>
        public bool Intersects(ref Ray ray, out float distance)
        {
            // Put ray in box space
            Matrix4x4F invTrans;
            Matrix4x4F rot = Matrix4x4F.RotationQuaternion(Orientation) * Matrix4x4F.Translation(Center);
            Matrix4x4F.Invert(ref rot, out invTrans);

            Ray bRay;
            Vector3F.TransformNormal(ref ray.Direction, ref invTrans, out bRay.Direction);
            Vector3F.TransformCoordinate(ref ray.Position, ref invTrans, out bRay.Position);

            //Perform a regular ray to BoundingBox check
            var bb = new BoundingBox(-HalfExtent, HalfExtent);
            var intersects = Collision.RayIntersectsBox(ref bRay, ref bb, out distance);

            return intersects;
        }

        /// <summary>
        /// Classify this bounding box as entirely in front of, in back of, or
        /// intersecting the given plane.
        /// </summary>
        public PlaneIntersectionType Intersects(ref Plane plane)
        {
            float dist = Plane.DotCoordinate(plane, Center);

            // Transform the plane's normal into this box's space
            Vector3F localNormal = Vector3F.Transform(plane.Normal, QuaternionF.Conjugate(Orientation));

            // Project the axes of the box onto the normal of the plane.  Half the
            // length of the projection (sometime called the "radius") is equal to
            // h(u) * abs(n dot b(u))) + h(v) * abs(n dot b(v)) + h(w) * abs(n dot b(w))
            // where h(i) are extents of the box, n is the plane normal, and b(i) are the 
            // axes of the box.
            float r = Math.Abs(HalfExtent.X * localNormal.X)
                    + Math.Abs(HalfExtent.Y * localNormal.Y)
                    + Math.Abs(HalfExtent.Z * localNormal.Z);

            if (dist > r)
            {
                return PlaneIntersectionType.Front;
            }
            else if (dist < -r)
            {
                return PlaneIntersectionType.Back;
            }
            else
            {
                return PlaneIntersectionType.Intersecting;
            }
        }

        #endregion

        #region Helper methods

        /// <summary>
        /// Return the 8 corner positions of this bounding box.
        ///
        ///     ZMax    ZMin
        ///    0----1  4----5
        ///    |    |  |    |
        ///    |    |  |    |
        ///    3----2  7----6
        ///
        /// The ordering of indices is a little strange to match what BoundingBox.GetCorners() does.
        /// </summary>
        public Vector3F[] GetCorners()
        {
            Vector3F[] corners = new Vector3F[CornerCount];
            GetCorners(corners, 0);
            return corners;
        }

        /// <summary>
        /// Get the axis-aligned <see cref="BoundingBox"/> which contains all <see cref="OrientedBoundingBox"/> corners.
        /// </summary>
        /// <returns>The axis-aligned BoundingBox of this OrientedBoundingBox.</returns>
        public BoundingBox GetBoundingBox()
        {
            return BoundingBox.FromPoints(GetCorners());
        }

        /// <summary>
        /// Return the 8 corner positions of this bounding box.
        ///
        ///    Back    Front
        ///     ZMax    ZMin
        ///    0----1  4----5
        ///    |    |  |    |
        ///    |    |  |    |
        ///    3----2  7----6
        ///
        /// The ordering of indices is a little strange to match what BoundingBox.GetCorners() does.
        /// </summary>
        /// <param name="corners">Array to fill with the eight corner positions</param>
        /// <param name="startIndex">Index within corners array to start writing positions</param>
        public void GetCorners(Vector3F[] corners, int startIndex)
        {

            Matrix4x4F m = Matrix4x4F.RotationQuaternion(Orientation);
            //Vector3F hX = m.Left * HalfExtent.X;
            //Vector3F hY = m.Up * HalfExtent.Y;
            //Vector3F hZ = m.Backward * HalfExtent.Z;

            Vector3F hX = m.Right * HalfExtent.X;
            Vector3F hY = m.Up * HalfExtent.Y;
            Vector3F hZ = m.Forward * HalfExtent.Z;

            int i = startIndex;
            corners[i++] = Center - hX + hY + hZ;
            corners[i++] = Center + hX + hY + hZ;
            corners[i++] = Center + hX - hY + hZ;
            corners[i++] = Center - hX - hY + hZ;
            corners[i++] = Center - hX + hY - hZ;
            corners[i++] = Center + hX + hY - hZ;
            corners[i++] = Center + hX - hY - hZ;
            corners[i++] = Center - hX - hY - hZ;

        }


        /// <summary>
        /// Determine whether the box described by half-extents hA, axis-aligned and centered at the origin, contains
        /// the box described by half-extents hB, whose position and orientation are given by the transform matrix mB.
        /// The matrix is assumed to contain only rigid motion; if it contains scaling or perpsective the result of
        /// this method will be incorrect.
        /// </summary>
        /// <param name="hA">Half-extents of first box</param>
        /// <param name="hB">Half-extents of second box</param>
        /// <param name="mB">Position and orientation of second box relative to first box</param>
        /// <returns>ContainmentType enum indicating whether the boxes are disjoin, intersecting, or
        /// whether box A contains box B.</returns>
        public static ContainmentType ContainsRelativeBox(ref Vector3F hA, ref Vector3F hB, ref Matrix4x4F mB)
        {
            Vector3F mB_T = mB.TranslationVector;
            Vector3F mB_TA = new Vector3F(Math.Abs(mB_T.X), Math.Abs(mB_T.Y), Math.Abs(mB_T.Z));

            // Transform the extents of B
            // TODO: check which coords Right/Up/Back refer to and access them directly. This looks dumb.
            Vector3F bX = mB.Right;      // x-axis of box B
            Vector3F bY = mB.Up;         // y-axis of box B
            Vector3F bZ = mB.Backward;   // z-axis of box B
            Vector3F hx_B = bX * hB.X;   // x extent of box B
            Vector3F hy_B = bY * hB.Y;   // y extent of box B
            Vector3F hz_B = bZ * hB.Z;   // z extent of box B

            // Check for containment first.
            float projx_B = Math.Abs(hx_B.X) + Math.Abs(hy_B.X) + Math.Abs(hz_B.X);
            float projy_B = Math.Abs(hx_B.Y) + Math.Abs(hy_B.Y) + Math.Abs(hz_B.Y);
            float projz_B = Math.Abs(hx_B.Z) + Math.Abs(hy_B.Z) + Math.Abs(hz_B.Z);
            if (mB_TA.X + projx_B <= hA.X && mB_TA.Y + projy_B <= hA.Y && mB_TA.Z + projz_B <= hA.Z)
                return ContainmentType.Contains;

            // Check for separation along the faces of the other box,
            // by projecting each local axis onto the other boxes' axes
            // http://www.cs.unc.edu/~geom/theses/gottschalk/main.pdf
            //
            // The general test form, given a choice of separating axis, is:
            //      sizeA = abs(dot(A.e1,axis)) + abs(dot(A.e2,axis)) + abs(dot(A.e3,axis))
            //      sizeB = abs(dot(B.e1,axis)) + abs(dot(B.e2,axis)) + abs(dot(B.e3,axis))
            //      distance = abs(dot(B.center - A.center),axis))
            //      if distance >= sizeA+sizeB, the boxes are disjoint
            //
            // We need to do this test on 15 axes:
            //      x, y, z axis of box A
            //      x, y, z axis of box B
            //      (v1 cross v2) for each v1 in A's axes, for each v2 in B's axes
            //
            // Since we're working in a space where A is axis-aligned and A.center=0, many
            // of the tests and products simplify away.

            // Check for separation along the axes of box A
            if (mB_TA.X >= hA.X + Math.Abs(hx_B.X) + Math.Abs(hy_B.X) + Math.Abs(hz_B.X))
                return ContainmentType.Disjoint;

            if (mB_TA.Y >= hA.Y + Math.Abs(hx_B.Y) + Math.Abs(hy_B.Y) + Math.Abs(hz_B.Y))
                return ContainmentType.Disjoint;

            if (mB_TA.Z >= hA.Z + Math.Abs(hx_B.Z) + Math.Abs(hy_B.Z) + Math.Abs(hz_B.Z))
                return ContainmentType.Disjoint;

            // Check for separation along the axes box B, hx_B/hy_B/hz_B
            if (Math.Abs(Vector3F.Dot(mB_T, bX)) >= Math.Abs(hA.X * bX.X) + Math.Abs(hA.Y * bX.Y) + Math.Abs(hA.Z * bX.Z) + hB.X)
                return ContainmentType.Disjoint;

            if (Math.Abs(Vector3F.Dot(mB_T, bY)) >= Math.Abs(hA.X * bY.X) + Math.Abs(hA.Y * bY.Y) + Math.Abs(hA.Z * bY.Z) + hB.Y)
                return ContainmentType.Disjoint;

            if (Math.Abs(Vector3F.Dot(mB_T, bZ)) >= Math.Abs(hA.X * bZ.X) + Math.Abs(hA.Y * bZ.Y) + Math.Abs(hA.Z * bZ.Z) + hB.Z)
                return ContainmentType.Disjoint;

            // Check for separation in plane containing an axis of box A and and axis of box B
            //
            // We need to compute all 9 cross products to find them, but a lot of terms drop out
            // since we're working in A's local space. Also, since each such plane is parallel
            // to the defining axis in each box, we know those dot products will be 0 and can
            // omit them.
            Vector3F axis;

            // a.X ^ b.X = (1,0,0) ^ bX
            axis = new Vector3F(0, -bX.Z, bX.Y);
            if (Math.Abs(Vector3F.Dot(mB_T, axis)) >= Math.Abs(hA.Y * axis.Y) + Math.Abs(hA.Z * axis.Z) + Math.Abs(Vector3F.Dot(axis, hy_B)) + Math.Abs(Vector3F.Dot(axis, hz_B)))
                return ContainmentType.Disjoint;

            // a.X ^ b.Y = (1,0,0) ^ bY
            axis = new Vector3F(0, -bY.Z, bY.Y);
            if (Math.Abs(Vector3F.Dot(mB_T, axis)) >= Math.Abs(hA.Y * axis.Y) + Math.Abs(hA.Z * axis.Z) + Math.Abs(Vector3F.Dot(axis, hz_B)) + Math.Abs(Vector3F.Dot(axis, hx_B)))
                return ContainmentType.Disjoint;

            // a.X ^ b.Z = (1,0,0) ^ bZ
            axis = new Vector3F(0, -bZ.Z, bZ.Y);
            if (Math.Abs(Vector3F.Dot(mB_T, axis)) >= Math.Abs(hA.Y * axis.Y) + Math.Abs(hA.Z * axis.Z) + Math.Abs(Vector3F.Dot(axis, hx_B)) + Math.Abs(Vector3F.Dot(axis, hy_B)))
                return ContainmentType.Disjoint;

            // a.Y ^ b.X = (0,1,0) ^ bX
            axis = new Vector3F(bX.Z, 0, -bX.X);
            if (Math.Abs(Vector3F.Dot(mB_T, axis)) >= Math.Abs(hA.Z * axis.Z) + Math.Abs(hA.X * axis.X) + Math.Abs(Vector3F.Dot(axis, hy_B)) + Math.Abs(Vector3F.Dot(axis, hz_B)))
                return ContainmentType.Disjoint;

            // a.Y ^ b.Y = (0,1,0) ^ bY
            axis = new Vector3F(bY.Z, 0, -bY.X);
            if (Math.Abs(Vector3F.Dot(mB_T, axis)) >= Math.Abs(hA.Z * axis.Z) + Math.Abs(hA.X * axis.X) + Math.Abs(Vector3F.Dot(axis, hz_B)) + Math.Abs(Vector3F.Dot(axis, hx_B)))
                return ContainmentType.Disjoint;

            // a.Y ^ b.Z = (0,1,0) ^ bZ
            axis = new Vector3F(bZ.Z, 0, -bZ.X);
            if (Math.Abs(Vector3F.Dot(mB_T, axis)) >= Math.Abs(hA.Z * axis.Z) + Math.Abs(hA.X * axis.X) + Math.Abs(Vector3F.Dot(axis, hx_B)) + Math.Abs(Vector3F.Dot(axis, hy_B)))
                return ContainmentType.Disjoint;

            // a.Z ^ b.X = (0,0,1) ^ bX
            axis = new Vector3F(-bX.Y, bX.X, 0);
            if (Math.Abs(Vector3F.Dot(mB_T, axis)) >= Math.Abs(hA.X * axis.X) + Math.Abs(hA.Y * axis.Y) + Math.Abs(Vector3F.Dot(axis, hy_B)) + Math.Abs(Vector3F.Dot(axis, hz_B)))
                return ContainmentType.Disjoint;

            // a.Z ^ b.Y = (0,0,1) ^ bY
            axis = new Vector3F(-bY.Y, bY.X, 0);
            if (Math.Abs(Vector3F.Dot(mB_T, axis)) >= Math.Abs(hA.X * axis.X) + Math.Abs(hA.Y * axis.Y) + Math.Abs(Vector3F.Dot(axis, hz_B)) + Math.Abs(Vector3F.Dot(axis, hx_B)))
                return ContainmentType.Disjoint;

            // a.Z ^ b.Z = (0,0,1) ^ bZ
            axis = new Vector3F(-bZ.Y, bZ.X, 0);
            if (Math.Abs(Vector3F.Dot(mB_T, axis)) >= Math.Abs(hA.X * axis.X) + Math.Abs(hA.Y * axis.Y) + Math.Abs(Vector3F.Dot(axis, hx_B)) + Math.Abs(Vector3F.Dot(axis, hy_B)))
                return ContainmentType.Disjoint;

            return ContainmentType.Intersects;
        }

        /// <summary>
        /// Convert this OrientedBoundingBox to a BoundingFrustum describing the same volume.
        ///
        /// A BoundingFrustum is defined by the matrix that carries its volume to the
        /// box from (-1,-1,0) to (1,1,1), so we just need a matrix that carries our box there.
        /// </summary>
        public BoundingFrustum ConvertToFrustum()
        {
            QuaternionF invOrientation;
            QuaternionF.Conjugate(ref Orientation, out invOrientation);
            float sx = 1.0f / HalfExtent.X;
            float sy = 1.0f / HalfExtent.Y;
            float sz = .5f / HalfExtent.Z;
            Matrix4x4F temp;
            Matrix4x4F.RotationQuaternion(ref invOrientation, out temp);
            temp.M11 *= sx; temp.M21 *= sx; temp.M31 *= sx;
            temp.M12 *= sy; temp.M22 *= sy; temp.M32 *= sy;
            temp.M13 *= sz; temp.M23 *= sz; temp.M33 *= sz;
            temp.TranslationVector = Vector3F.UnitZ * 0.5f + Vector3F.TransformNormal(-Center, temp);

            return new BoundingFrustum(temp);
        }
        #endregion

        public static OrientedBoundingBox Merge(ref OrientedBoundingBox obb1, ref OrientedBoundingBox obb2)
        {
            var corners1 = obb1.GetCorners();
            var corners2 = obb2.GetCorners();
            Vector3F[] points = new Vector3F[16];
            Array.Copy(corners1, points, 8);
            Array.Copy(corners2, 0, points, 8, 8);

            return FromPoints(points);

        }

        public static OrientedBoundingBox Merge(ref OrientedBoundingBox obb1, Vector3F[] corners)
        {
            var corners1 = obb1.GetCorners();
            var points = new Vector3F[16];
            Array.Copy(corners1, points, 8);
            Array.Copy(corners, 0, points, 8, 8);

            return FromPoints(points);

        }
        
        public static OrientedBoundingBox Merge(ref OrientedBoundingBox obb1, Vector3[] corners)
        {
            var corners1 = obb1.GetCorners();
            var points = new Vector3[16];
            Array.Copy(corners1, points, 8);
            Array.Copy(corners, 0, points, 8, 8);

            return FromPoints(points);

        }
    }
}
