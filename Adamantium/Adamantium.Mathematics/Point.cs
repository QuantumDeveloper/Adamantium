using System;
using System.Runtime.InteropServices;

namespace Adamantium.Mathematics
{
   /// <summary>
   /// Describes a point
   /// </summary>
   [StructLayout(LayoutKind.Sequential)]
   public struct Point
   {
      /// <summary>
      /// X coordinate
      /// </summary>
      public Double X { get; set; }

      /// <summary>
      /// Y coordinate
      /// </summary>
      public Double Y { get; set; }

      /// <summary>
      /// Creates new instance of <see cref="Point"/>
      /// </summary>
      /// <param name="x"></param>
      /// <param name="y"></param>
      public Point(Double x, Double y)
      {
         X = x;
         Y = y;
      }

      public Point(Single x, Single y)
      {
         X = x;
         Y = y;
      }

      /// <summary>
      /// Creates new instance of <see cref="Point"/>
      /// </summary>
      /// <param name="p"></param>
      public Point(Double p)
      {
         X = p;
         Y = p;
      }

      /// <summary>
      /// Creates new instance of <see cref="Point"/>
      /// </summary>
      /// <param name="p"></param>
      public Point(Point p)
      {
         X = p.X;
         Y = p.Y;
      }


      public Double Length()
      {
         return (float)Math.Sqrt((X * X) + (Y * Y));
      }

      public static readonly Point Zero = new Point(0);

      /// <summary>
      /// Subtracts 2 points
      /// </summary>
      /// <param name="point1">first point</param>
      /// <param name="point2">second point</param>
      /// <returns>returns new <see cref="Point"/></returns>
      public static Point operator - (Point point1, Point point2)
      {
         return new Point(point1.X - point2.X, point1.Y - point2.Y);
      }

      /// <summary>
      /// Adds 2 points
      /// </summary>
      /// <param name="point1">first point</param>
      /// <param name="point2">second point</param>
      /// <returns>returns new <see cref="Point"/></returns>
      public static Point operator +(Point point1, Point point2)
      {
         return new Point(point1.X + point2.X, point1.Y + point2.Y);
      }

      /// <summary>
      /// Applies offset to the <see cref="Point"/>
      /// </summary>
      /// <param name="p">The point</param>
      /// <param name="offset">offset</param>
      /// <returns>returns new <see cref="Point"/></returns>
      public static Point operator -(Point p, double offset)
      {
         return new Point(p.X - offset, p.Y - offset);
      }

      /// <summary>
      /// Applies offset to the <see cref="Point"/>
      /// </summary>
      /// <param name="p">The point</param>
      /// <param name="offset">offset</param>
      /// <returns>returns new <see cref="Point"/></returns>
      public static Point operator +(Point p, double offset)
      {
         return new Point(p.X+offset, p.Y+offset);
      }

      /// <summary>
      /// Subtracts <see cref="Vector2D"/> from the <see cref="Point"/>
      /// </summary>
      /// <param name="p">The point</param>
      /// <param name="offset">offset</param>
      /// <returns>returns new <see cref="Point"/></returns>
      public static Point operator -(Point p, Vector2D offset)
      {
         return new Point(p.X - offset.X, p.Y - offset.Y);
      }

      /// <summary>
      /// Adds <see cref="Vector2D"/> to the <see cref="Point"/>
      /// </summary>
      /// <param name="p">The point</param>
      /// <param name="offset">offset</param>
      /// <returns>returns new <see cref="Point"/></returns>
      public static Point operator +(Point p, Vector2D offset)
      {
         return new Point(p.X + offset.X, p.Y + offset.Y);
      }

      /// <summary>
      /// Divide point by factor 
      /// </summary>
      /// <param name="point">point</param>
      /// <param name="factor">factor</param>
      /// <returns>returns new <see cref="Point"/></returns>
      public static Point operator /(Point point, double factor)
      {
         return new Point(point.X/factor, point.Y/factor);
      }

      /// <summary>
      /// Multiply point by factor 
      /// </summary>
      /// <param name="point">point</param>
      /// <param name="factor">factor</param>
      /// <returns>returns new <see cref="Point"/></returns>
      public static Point operator *(Point point, double factor)
      {
         return new Point(point.X * factor, point.Y * factor);
      }

      /// <summary>
      /// Convert <see cref="Point"/> to a <see cref="Vector2D"/>
      /// </summary>
      /// <param name="point">point to convert</param>
      /// <returns>returns new <see cref="Vector2D"/></returns>
      public static implicit operator Vector2F(Point point)
      {
         return new Vector2F((float)point.X, (float)point.Y);
      }

      /// <summary>
      /// Convert <see cref="Point"/> to a <see cref="Vector3D"/>
      /// </summary>
      /// <param name="point"></param>
      /// <returns>returns new <see cref="Vector3D"/></returns>
      public static implicit operator Vector3F(Point point)
      {
         return new Vector3F((float)point.X, (float)point.Y, 0);
      }


      /// <summary>
      /// Convert <see cref="Point"/> to a <see cref="Vector2D"/>
      /// </summary>
      /// <param name="point">point to convert</param>
      /// <returns>returns new <see cref="Vector2D"/></returns>
      public static implicit operator Vector2D(Point point)
      {
         return new Vector2D(point.X, point.Y);
      }

      /// <summary>
      /// Convert <see cref="Point"/> to a <see cref="Vector3D"/>
      /// </summary>
      /// <param name="point"></param>
      /// <returns>returns new <see cref="Vector3D"/></returns>
      public static implicit operator Vector3D(Point point)
      {
         return new Vector3D(point.X, point.Y, 0);
      }

      /// <summary>
      /// Comparison operator for 2 Points
      /// </summary>
      /// <param name="point1">first point</param>
      /// <param name="point2">second point</param>
      /// <returns>returns true if both Points are equals</returns>
      public static bool operator ==(Point point1, Point point2)
      {
         return point1.X == point2.X && point1.Y == point2.Y;
      }

      /// <summary>
      /// Comparison operator for 2 Points
      /// </summary>
      /// <param name="point1">first point</param>
      /// <param name="point2">second point</param>
      /// <returns>returns true if both Points are NOT equals</returns>
      public static bool operator !=(Point point1, Point point2)
      {
         return point1.X != point2.X && point1.Y != point2.Y;
      }

      /// <summary>
      /// Indicates whether this instance and a specified object are equal.
      /// </summary>
      /// <returns>
      /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false. 
      /// </returns>
      /// <param name="obj">The object to compare with the current instance. </param><filterpriority>2</filterpriority>
      public override bool Equals(object obj)
      {
         if (obj is Point)
         {
            var other = (Point)obj;
            return other.X == X && other.Y == Y;
         }

         return false;
      }

      /// <summary>
      /// Returns the hash code for this instance.
      /// </summary>
      /// <returns>
      /// A 32-bit signed integer that is the hash code for this instance.
      /// </returns>
      /// <filterpriority>2</filterpriority>
      public override int GetHashCode()
      {
         unchecked
         {
            int hash = 457;
            hash = (hash * 98) ^ X.GetHashCode();
            hash = (hash * 98) ^ Y.GetHashCode();
            return hash;
         }
      }

      /// <summary>
      /// Returns the fully qualified type name of this instance.
      /// </summary>
      /// <returns>
      /// A <see cref="T:System.String"/> containing a fully qualified type name.
      /// </returns>
      /// <filterpriority>2</filterpriority>
      public override string ToString() => "X: "+X + " Y: "+Y;
   }
}
