using System;

namespace Adamantium.Mathematics
{
   public struct Bounds
   {
      public Vector3 Center;

      public Vector3 HalfExtent;

      public Vector3 Size => HalfExtent * 2;

      public double Width => Size.X;

      public double Height => Size.Y;

      public double Depth => Size.Z;

      public double Radius;

      public double Diameter => Radius * Radius;

      public Quaternion Orientation;

      public Bounds(Vector3 center, Vector3 halfExtents, Quaternion rotation)
      {
         Center = center;
         HalfExtent = halfExtents;
         Radius = Vector3F.Max(halfExtents);
         Orientation = rotation;
      }

      public static Bounds FromPoints(Vector3[] points)
      {
         if (points == null || points.Length == 0)
            throw new ArgumentNullException(nameof(points));

         var minimum = new Vector3(float.MaxValue);
         var maximum = new Vector3(float.MinValue);

         for (int i = 0; i < points.Length; ++i)
         {
            Vector3.Min(ref minimum, ref points[i], out minimum);
            Vector3.Max(ref maximum, ref points[i], out maximum);
         }

         var center = minimum + (maximum - minimum) / 2f;
         var halfExtent = maximum - center;

         return new Bounds(center, halfExtent, QuaternionF.Identity);
      }
      
      public Bounds Transform(Vector3 scale, Quaternion rotation, Vector3 translation)
      {
         return new Bounds(Vector3.Transform(Center * scale, rotation) + translation, HalfExtent * scale, Orientation * rotation);
      }

      public Vector3[] GetCorners()
      {
         var corners = new Vector3[8];

         var m = Matrix4x4.RotationQuaternion(Orientation);

         var hX = m.Right * HalfExtent.X;
         var hY = m.Up * HalfExtent.Y;
         var hZ = m.Forward * HalfExtent.Z;

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
         return new BoundingSphere(bounds.Center, (float)bounds.Radius);
      }

      public static Bounds FromBoundingBox(OrientedBoundingBox obb)
      {
         return FromBoundingBox(ref obb);
      }

      public static Bounds FromBoundingBox(ref OrientedBoundingBox obb)
      {
         return new Bounds((Vector3)obb.Center, (Vector3)obb.HalfExtent, obb.Orientation);
      }

      public static Bounds FromBoundingSphere(BoundingSphere sphere)
      {
         return FromBoundingSphere(ref sphere);
      }

      public static Bounds FromBoundingSphere(ref BoundingSphere sphere)
      {
         return new Bounds((Vector3)sphere.Center, new Vector3(sphere.Radius), QuaternionF.Identity);
      }

      public override string ToString()
      {
         return $"Center: {Center}, HalfExtent: {HalfExtent}, Radius: {Radius}, Orientation: {Orientation}";
      }
   }
}
