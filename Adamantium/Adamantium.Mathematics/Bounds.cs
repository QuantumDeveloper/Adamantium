using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adamantium.Mathematics
{
   public struct Bounds
   {
      public Vector3F Center;

      public Vector3F HalfExtent;

      public Vector3F Size=> HalfExtent * 2;

      public float Width => Size.X;

      public float Height => Size.Y;

      public float Depth => Size.Z;

      public float Radius;

      public float Diameter => Radius * Radius;

      public QuaternionF Orientation;

      public Bounds(Vector3F center, Vector3F halfExtents, QuaternionF rotation)
      {
         Center = center;
         HalfExtent = halfExtents;
         Radius = Vector3F.Max(halfExtents);
         Orientation = rotation;
      }

      public static Bounds FromPoints(Vector3F[] points)
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

         return new Bounds(center, halfExtent, QuaternionF.Identity);
      }

      public Bounds Transform(Vector3F scale, QuaternionF rotation, Vector3F translation)
      {
         return new Bounds(Vector3F.Transform(Center * scale, rotation) + translation, HalfExtent * scale, Orientation * rotation);
      }

      public Vector3F[] GetCorners()
      {
         Vector3F[] corners = new Vector3F[8];

         Matrix4x4F m = Matrix4x4F.RotationQuaternion(Orientation);
         //Vector3F hX = m.Left * HalfExtent.X;
         //Vector3F hY = m.Up * HalfExtent.Y;
         //Vector3F hZ = m.Backward * HalfExtent.Z;

         Vector3F hX = m.Right * HalfExtent.X;
         Vector3F hY = m.Up * HalfExtent.Y;
         Vector3F hZ = m.Forward * HalfExtent.Z;

         int i = 0;
         corners[i++] = Center - hX + hY + hZ;
         corners[i++] = Center + hX + hY + hZ;
         corners[i++] = Center + hX - hY + hZ;
         corners[i++] = Center - hX - hY + hZ;
         corners[i++] = Center - hX + hY - hZ;
         corners[i++] = Center + hX + hY - hZ;
         corners[i++] = Center + hX - hY - hZ;
         corners[i++] = Center - hX - hY - hZ;

         return corners;
      }

      public static implicit operator BoundingBox(Bounds bounds)
      {
         var minimum = bounds.Center - bounds.HalfExtent;
         var maximum = bounds.Center + bounds.HalfExtent;
         return new BoundingBox(minimum, maximum);
      }

      public static implicit operator OrientedBoundingBox(Bounds bounds)
      {
         return new OrientedBoundingBox(bounds.Center, bounds.HalfExtent, QuaternionF.Identity);
      }

      public static implicit operator BoundingSphere(Bounds bounds)
      {
         return new BoundingSphere(bounds.Center, bounds.Radius);
      }

      public static Bounds FromBoundingBox(OrientedBoundingBox obb)
      {
         return FromBoundingBox(ref obb);
      }

      public static Bounds FromBoundingBox(ref OrientedBoundingBox obb)
      {
         return new Bounds(obb.Center, obb.HalfExtent, obb.Orientation);
      }

      public static Bounds FromBoundingSphere(BoundingSphere sphere)
      {
         return FromBoundingSphere(ref sphere);
      }

      public static Bounds FromBoundingSphere(ref BoundingSphere sphere)
      {
         return new Bounds(sphere.Center, new Vector3F(sphere.Radius), QuaternionF.Identity);
      }

      public override string ToString()
      {
         return $"Center: {Center}, HalfExtent: {HalfExtent}, Radius: {Radius}, Orientation: {Orientation}";
      }
   }
}
