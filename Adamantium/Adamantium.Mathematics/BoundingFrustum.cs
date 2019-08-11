using System;
using System.Text;

namespace Adamantium.Mathematics
{
   public class BoundingFrustum:IEquatable<BoundingFrustum>
   {
      #region Private Fields

      private Matrix4x4F matrix;
      private readonly Vector3F[] corners = new Vector3F[CornerCount];
      private readonly Plane[] planes = new Plane[PlaneCount];

      private const int PlaneCount = 6;

      #endregion Private Fields

      #region Public Fields
      public const int CornerCount = 8;
      #endregion

      #region Public Constructors

      public BoundingFrustum(Matrix4x4F value, bool enableFarPlaneCheck = false)
      {
         this.matrix = value;
         EnableFarPlaneCheck = enableFarPlaneCheck;
         this.CreatePlanes();
         this.CreateCorners();
      }

      #endregion Public Constructors

      #region Public Properties

      public Matrix4x4F Matrix4x4F
      {
         get { return this.matrix; }
         set
         {
            this.matrix = value;
            this.CreatePlanes();    // FIXME: The odds are the planes will be used a lot more often than the matrix
            this.CreateCorners();   // is updated, so this should help performance. I hope ;)
         }
      }

      public Plane Near => this.planes[0];

      public Plane Far => this.planes[1];

      public Plane Left => this.planes[2];

      public Plane Right => this.planes[3];

      public Plane Top => this.planes[4];

      public Plane Bottom => this.planes[5];

      public Boolean EnableFarPlaneCheck { get; set; }

      #endregion Public Properties

      #region Public Methods

      public static bool operator ==(BoundingFrustum a, BoundingFrustum b)
      {
         if (object.Equals(a, null))
            return (object.Equals(b, null));

         if (object.Equals(b, null))
            return (object.Equals(a, null));

         return a.matrix == (b.matrix);
      }

      public static bool operator !=(BoundingFrustum a, BoundingFrustum b)
      {
         return !(a == b);
      }

      public ContainmentType Contains(BoundingBox box)
      {
         var result = default(ContainmentType);
         this.Contains(ref box, out result);
         return result;
      }

      public void Contains(ref BoundingBox box, out ContainmentType result)
      {
         Vector3F p, n;
         Plane plane;
         result = ContainmentType.Contains;
         for (int i = 0; i < PlaneCount; i++)
         {
            plane = planes[i];
            if (!EnableFarPlaneCheck && plane == Far)
            {
               continue;
            }
            
            GetBoxToPlanePVertexNVertex(ref box, ref plane.Normal, out p, out n);
            if (Collision.PlaneIntersectsPoint(ref plane, ref p) == PlaneIntersectionType.Back)
            {
               result = ContainmentType.Disjoint;
               return;
            }

            if (Collision.PlaneIntersectsPoint(ref plane, ref n) == PlaneIntersectionType.Back)
            {
               result = ContainmentType.Intersects;
               return;
            }
         }
      }

      private void GetBoxToPlanePVertexNVertex(ref BoundingBox box, ref Vector3F planeNormal, out Vector3F p, out Vector3F n)
      {
         p = box.Minimum;
         if (planeNormal.X >= 0)
            p.X = box.Maximum.X;
         if (planeNormal.Y >= 0)
            p.Y = box.Maximum.Y;
         if (planeNormal.Z >= 0)
            p.Z = box.Maximum.Z;

         n = box.Maximum;
         if (planeNormal.X >= 0)
            n.X = box.Minimum.X;
         if (planeNormal.Y >= 0)
            n.Y = box.Minimum.Y;
         if (planeNormal.Z >= 0)
            n.Z = box.Minimum.Z;
      }

      public ContainmentType Contains(BoundingFrustum frustum)
      {
         if (this == frustum)                // We check to see if the two frustums are equal
            return ContainmentType.Contains;// If they are, there's no need to go any further.

         var intersects = false;
         for (var i = 0; i < PlaneCount; ++i)
         {
            if (!EnableFarPlaneCheck && planes[i] == Far)
            {
               continue;
            }
            PlaneIntersectionType planeIntersectionType;
            frustum.Intersects(ref planes[i], out planeIntersectionType);
            switch (planeIntersectionType)
            {
               case PlaneIntersectionType.Front:
                  return ContainmentType.Disjoint;
               case PlaneIntersectionType.Intersecting:
                  intersects = true;
                  break;
            }
         }
         return intersects ? ContainmentType.Intersects : ContainmentType.Contains;
      }

      public ContainmentType Contains(BoundingSphere sphere)
      {
         ContainmentType result;
         this.Contains(ref sphere, out result);
         return result;
      }

      public void Contains(ref BoundingSphere sphere, out ContainmentType result)
      {
         var generalResult = PlaneIntersectionType.Front;
         var planeResult = PlaneIntersectionType.Front;
         for (int i = 0; i < PlaneCount; i++)
         {
            if (!EnableFarPlaneCheck && planes[i] == Far)
            {
               continue;
            }
            switch (i)
            {
               case 0: planeResult = planes[0].Intersects(ref sphere); break;
               case 1: planeResult = planes[1].Intersects(ref sphere); break;
               case 2: planeResult = planes[2].Intersects(ref sphere); break;
               case 3: planeResult = planes[3].Intersects(ref sphere); break;
               case 4: planeResult = planes[4].Intersects(ref sphere); break;
               case 5: planeResult = planes[5].Intersects(ref sphere); break;
            }
            switch (planeResult)
            {
               case PlaneIntersectionType.Back:
                  result = ContainmentType.Disjoint;
                  return;
               case PlaneIntersectionType.Intersecting:
                  generalResult = PlaneIntersectionType.Intersecting;
                  break;
            }
         }
         switch (generalResult)
         {
            case PlaneIntersectionType.Intersecting:
               result = ContainmentType.Intersects;
               return;
            default:
               result = ContainmentType.Contains;
               return;
         }
      }

      public ContainmentType Contains(Vector3F[] points)
      {
         for (int p = 0; p < PlaneCount; p++)
         {
            bool cont = false;
            if (!EnableFarPlaneCheck && planes[p] == Far)
            {
               continue;
            }
            for (int k = 0; k < points.Length; k++)
            {
               if (planes[p][0] * points[k].X + planes[p][1] * points[k].Y + planes[p][2] * points[k].Z + planes[p][3] >= 0)
               {
                  cont = true;
                  break;
               }
            }
            if (cont) continue;
            return ContainmentType.Disjoint;
         }
         return ContainmentType.Contains;
      }

      public ContainmentType Contains(ref Vector3F point)
      {
         for (var i = 0; i < PlaneCount; ++i)
         {
            if (!EnableFarPlaneCheck && planes[i] == Far)
            {
               continue;
            }
            if (planes[i][0] * point.X + planes[i][1] * point.Y + planes[i][2] * point.Z + planes[i][3] >= 0)
            {
               continue;
            }
            return ContainmentType.Disjoint;
         }
         return ContainmentType.Contains;
      }

      public bool Equals(BoundingFrustum other)
      {
         return (this == other);
      }

      public override bool Equals(object obj)
      {
         BoundingFrustum f = obj as BoundingFrustum;
         return (!object.Equals(f, null)) && (this == f);
      }

      public Vector3F[] GetCorners()
      {
         return (Vector3F[])this.corners.Clone();
      }

      public void GetCorners(Vector3F[] corners)
      {
         if (corners == null) throw new ArgumentNullException(nameof(corners));
         if (corners.Length < CornerCount) throw new ArgumentOutOfRangeException(nameof(corners));

         this.corners.CopyTo(corners, 0);
      }

      public override int GetHashCode()
      {
         return this.matrix.GetHashCode();
      }

      public bool Intersects(BoundingBox box)
      {
         var result = false;
         this.Intersects(ref box, out result);
         return result;
      }

      public void Intersects(ref BoundingBox box, out bool result)
      {
         ContainmentType containment;
         Contains(ref box, out containment);
         result = containment != ContainmentType.Disjoint;
      }

      public bool Intersects(BoundingFrustum frustum)
      {
         return Contains(frustum) != ContainmentType.Disjoint;
      }

      public bool Intersects(BoundingSphere sphere)
      {
         bool result;
         this.Intersects(ref sphere, out result);
         return result;
      }

      public void Intersects(ref BoundingSphere sphere, out bool result)
      {
         ContainmentType containment;
         this.Contains(ref sphere, out containment);
         result = containment != ContainmentType.Disjoint;
      }

      public PlaneIntersectionType Intersects(Plane plane)
      {
         PlaneIntersectionType result;
         Intersects(ref plane, out result);
         return result;
      }

      public void Intersects(ref Plane plane, out PlaneIntersectionType result)
      {
         result = plane.Intersects(ref corners[0]);
         for (int i = 1; i < corners.Length; i++)
            if (plane.Intersects(ref corners[i]) != result)
               result = PlaneIntersectionType.Intersecting;
      }

      public override string ToString()
      {
         StringBuilder sb = new StringBuilder(256);
         sb.Append("{Near:");
         sb.Append(this.planes[0].ToString());
         sb.Append(" Far:");
         sb.Append(this.planes[1].ToString());
         sb.Append(" Left:");
         sb.Append(this.planes[2].ToString());
         sb.Append(" Right:");
         sb.Append(this.planes[3].ToString());
         sb.Append(" Top:");
         sb.Append(this.planes[4].ToString());
         sb.Append(" Bottom:");
         sb.Append(this.planes[5].ToString());
         sb.Append("}");
         return sb.ToString();
      }

      #endregion Public Methods

      #region Private Methods

      private void CreateCorners()
      {
         IntersectionPoint(ref this.planes[0], ref this.planes[2], ref this.planes[4], out this.corners[0]);
         IntersectionPoint(ref this.planes[0], ref this.planes[3], ref this.planes[4], out this.corners[1]);
         IntersectionPoint(ref this.planes[0], ref this.planes[3], ref this.planes[5], out this.corners[2]);
         IntersectionPoint(ref this.planes[0], ref this.planes[2], ref this.planes[5], out this.corners[3]);
         IntersectionPoint(ref this.planes[1], ref this.planes[2], ref this.planes[4], out this.corners[4]);
         IntersectionPoint(ref this.planes[1], ref this.planes[3], ref this.planes[4], out this.corners[5]);
         IntersectionPoint(ref this.planes[1], ref this.planes[3], ref this.planes[5], out this.corners[6]);
         IntersectionPoint(ref this.planes[1], ref this.planes[2], ref this.planes[5], out this.corners[7]);
      }

      private void CreatePlanes()
      {
         planes[0][0] = matrix.M14 + matrix.M13;
         planes[0][1] = matrix.M24 + matrix.M23;
         planes[0][2] = matrix.M34 + matrix.M33;
         planes[0][3] = matrix.M44 + matrix.M43;
         planes[0].Normalize();

         planes[1][0] = matrix.M14 - matrix.M13;
         planes[1][1] = matrix.M24 - matrix.M23;
         planes[1][2] = matrix.M34 - matrix.M33;
         planes[1][3] = matrix.M44 - matrix.M43;
         planes[1].Normalize();

         planes[2][0] = matrix.M14 + matrix.M11;
         planes[2][1] = matrix.M24 + matrix.M21;
         planes[2][2] = matrix.M34 + matrix.M31;
         planes[2][3] = matrix.M44 + matrix.M41;
         planes[2].Normalize();

         planes[3][0] = matrix.M14 - matrix.M11;
         planes[3][1] = matrix.M24 - matrix.M21;
         planes[3][2] = matrix.M34 - matrix.M31;
         planes[3][3] = matrix.M44 - matrix.M41;
         planes[3].Normalize();

         planes[4][0] = matrix.M14 - matrix.M12;
         planes[4][1] = matrix.M24 - matrix.M22;
         planes[4][2] = matrix.M34 - matrix.M32;
         planes[4][3] = matrix.M44 - matrix.M42;
         planes[4].Normalize();

         planes[5][0] = matrix.M14 + matrix.M12;
         planes[5][1] = matrix.M24 + matrix.M22;
         planes[5][2] = matrix.M34 + matrix.M32;
         planes[5][3] = matrix.M44 + matrix.M42;
         planes[5].Normalize();

      }

      private static void IntersectionPoint(ref Plane a, ref Plane b, ref Plane c, out Vector3F result)
      {
         // Formula used
         //                d1 ( N2 * N3 ) + d2 ( N3 * N1 ) + d3 ( N1 * N2 )
         //P =   -------------------------------------------------------------------------
         //                             N1 . ( N2 * N3 )
         //
         // Note: N refers to the normal, d refers to the displacement. '.' means dot product. '*' means cross product

         Vector3F v1, v2, v3;
         Vector3F cross;

         Vector3F.Cross(ref b.Normal, ref c.Normal, out cross);

         float f;
         Vector3F.Dot(ref a.Normal, ref cross, out f);
         f *= -1.0f;

         Vector3F.Cross(ref b.Normal, ref c.Normal, out cross);
         Vector3F.Multiply(ref cross, a.D, out v1);
         //v1 = (a.D * (Vector3F.Cross(b.Normal, c.Normal)));


         Vector3F.Cross(ref c.Normal, ref a.Normal, out cross);
         Vector3F.Multiply(ref cross, b.D, out v2);
         //v2 = (b.D * (Vector3F.Cross(c.Normal, a.Normal)));


         Vector3F.Cross(ref a.Normal, ref b.Normal, out cross);
         Vector3F.Multiply(ref cross, c.D, out v3);
         //v3 = (c.D * (Vector3F.Cross(a.Normal, b.Normal)));

         result.X = (v1.X + v2.X + v3.X) / f;
         result.Y = (v1.Y + v2.Y + v3.Y) / f;
         result.Z = (v1.Z + v2.Z + v3.Z) / f;
      }

      private void NormalizePlane(ref Plane p)
      {
         float factor = 1f / p.Normal.Length();
         p.Normal.X *= factor;
         p.Normal.Y *= factor;
         p.Normal.Z *= factor;
         p.D *= factor;
      }

      #endregion
   }
}
