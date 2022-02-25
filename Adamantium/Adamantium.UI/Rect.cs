using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Adamantium.UI;

/// <summary>
/// Defines a rectangle, which can be transformed by a matrix
/// </summary>
public struct Rect
{
   /// <summary>
   /// An empty rectangle.
   /// </summary>
   public static readonly Rect Empty = default(Rect);

   /// <summary>
   /// Initializes a new instance of the <see cref="Rect"/> structure.
   /// </summary>
   /// <param name="x">The X position.</param>
   /// <param name="y">The Y position.</param>
   /// <param name="width">The width.</param>
   /// <param name="height">The height.</param>
   public Rect(double x, double y, double width, double height)
   {
      X = x;
      Y = y;
      Width = width;
      Height = height;
   }

   /// <summary>
   /// Initializes a new instance of the <see cref="Rect"/> structure.
   /// </summary>
   /// <param name="size">The size of the rectangle.</param>
   public Rect(Size size)
   {
      X = 0;
      Y = 0;
      Width = size.Width;
      Height = size.Height;
   }

   /// <summary>
   /// Initializes a new instance of the <see cref="Rect"/> structure.
   /// </summary>
   /// <param name="position">The position of the rectangle.</param>
   /// <param name="size">The size of the rectangle.</param>
   public Rect(Vector2 position, Size size)
   {
      X = position.X;
      Y = position.Y;
      Width = size.Width;
      Height = size.Height;
   }

   /// <summary>
   /// Initializes a new instance of the <see cref="Rect"/> structure.
   /// </summary>
   /// <param name="topLeft">The top left position of the rectangle.</param>
   /// <param name="bottomRight">The bottom right position of the rectangle.</param>
   public Rect(Vector2 topLeft, Vector2 bottomRight)
   {
      X = topLeft.X;
      Y = topLeft.Y;
      Width = bottomRight.X - topLeft.X;
      Height = bottomRight.Y - topLeft.Y;
   }

   /// <summary>
   /// Gets the X position.
   /// </summary>
   public double X { get; set; }

   /// <summary>
   /// Gets the Y position.
   /// </summary>
   public double Y { get; set; }

   /// <summary>
   /// Gets the width.
   /// </summary>
   public double Width { get; set; }

   /// <summary>
   /// Gets the height.
   /// </summary>
   public double Height { get; set; }

   /// <summary>
   /// Gets the position of the rectangle.
   /// </summary>
   public Vector2 Location => new Vector2(X, Y);

   /// <summary>
   /// Gets the size of the rectangle.
   /// </summary>
   public Size Size => new Size(Width, Height);

   /// <summary>
   /// Gets the right position of the rectangle.
   /// </summary>
   public double Right => X + Width;

   /// <summary>
   /// Gets the bottom position of the rectangle.
   /// </summary>
   public double Bottom => Y + Height;

   /// <summary>
   /// Gets the top left Vector2D of the rectangle.
   /// </summary>
   public Vector2 TopLeft => new Vector2(X, Y);

   /// <summary>
   /// Gets the top right Vector2D of the rectangle.
   /// </summary>
   public Vector2 TopRight => new Vector2(Right, Y);

   /// <summary>
   /// Gets the bottom left Vector2D of the rectangle.
   /// </summary>
   public Vector2 BottomLeft => new Vector2(X, Bottom);

   /// <summary>
   /// Gets the bottom right Vector2D of the rectangle.
   /// </summary>
   public Vector2 BottomRight => new Vector2(Right, Bottom);

   /// <summary>
   /// Gets the center Vector2D of the rectangle.
   /// </summary>
   public Vector2 Center => new Vector2(X + (Width / 2), Y + (Height / 2));

   /// <summary>
   /// Gets a value that indicates whether the rectangle is empty.
   /// </summary>
   public bool IsEmpty => Width == 0 && Height == 0;

   /// <summary>
   /// Checks for equality between two <see cref="Rect"/>s.
   /// </summary>
   /// <param name="left">The first rect.</param>
   /// <param name="right">The second rect.</param>
   /// <returns>True if the rects are equal; otherwise false.</returns>
   public static bool operator ==(Rect left, Rect right)
   {
      return left.Location == right.Location && left.Size == right.Size;
   }

   /// <summary>
   /// Checks for unequality between two <see cref="Rect"/>s.
   /// </summary>
   /// <param name="left">The first rect.</param>
   /// <param name="right">The second rect.</param>
   /// <returns>True if the rects are unequal; otherwise false.</returns>
   public static bool operator !=(Rect left, Rect right)
   {
      return !(left == right);
   }

   /// <summary>
   /// Multiplies a rectangle by a vector.
   /// </summary>
   /// <param name="rect">The rectangle.</param>
   /// <param name="scale">The vector scale.</param>
   /// <returns>The scaled rectangle.</returns>
   public static Rect operator *(Rect rect, Vector2 scale)
   {
      return new Rect(
         rect.X * scale.X,
         rect.Y * scale.Y,
         rect.Width * scale.X,
         rect.Height * scale.Y);
   }


   /*
   /// <summary>
   /// Transforms a rectangle by a matrix and returns the axis-aligned bounding box.
   /// </summary>
   /// <param name="rect">The rectangle.</param>
   /// <param name="matrix">The matrix.</param>
   /// <returns>The axis-aligned bounding box.</returns>
   public static Rect operator *(Rect rect, Matrix4x4F matrix)
   {
      return new Rect(rect.TopLeft * matrix, rect.BottomRight * matrix);
   }
   */


   /// <summary>
   /// Divides a rectangle by a vector.
   /// </summary>
   /// <param name="rect">The rectangle.</param>
   /// <param name="scale">The vector scale.</param>
   /// <returns>The scaled rectangle.</returns>
   public static Rect operator /(Rect rect, Vector2 scale)
   {
      return new Rect(
         rect.X / scale.X,
         rect.Y / scale.Y,
         rect.Width / scale.X,
         rect.Height / scale.Y);
   }

   /// <summary>
   /// Determines whether a Vector2Ds in in the bounds of the rectangle.
   /// </summary>
   /// <param name="p">The Vector2D.</param>
   /// <returns>true if the Vector2D is in the bounds of the rectangle; otherwise false.</returns>
   public bool Contains(Vector2 p)
   {
      return p.X >= X && p.X < X + Width &&
             p.Y >= Y && p.Y < Y + Height;
   }

   /// <summary>
   /// Centers another rectangle in this rectangle.
   /// </summary>
   /// <param name="rect">The rectangle to center.</param>
   /// <returns>The centered rectangle.</returns>
   public Rect CenterIn(Rect rect)
   {
      return new Rect(
         X + ((Width - rect.Width) / 2),
         Y + ((Height - rect.Height) / 2),
         rect.Width,
         rect.Height);
   }

   /// <summary>
   /// Inflates the rectangle.
   /// </summary>
   /// <param name="thickness">The thickness.</param>
   /// <returns>The inflated rectangle.</returns>
   public Rect Inflate(double thickness)
   {
      return new Rect(new Vector2(X - thickness, Y - thickness), Size.Inflate(new Thickness(thickness)));
   }

   /// <summary>
   /// Inflates the rectangle.
   /// </summary>
   /// <param name="thickness">The thickness.</param>
   /// <returns>The inflated rectangle.</returns>
   public Rect Inflate(Thickness thickness)
   {
      return new Rect(
         new Vector2(X - thickness.Left, Y - thickness.Top),
         Size.Inflate(thickness));
   }

   /// <summary>
   /// Deflates the rectangle.
   /// </summary>
   /// <param name="thickness">The thickness.</param>
   /// <returns>The deflated rectangle.</returns>
   /// <remarks>The deflated rectangle size cannot be less than 0.</remarks>
   public Rect Deflate(double thickness)
   {
      return Deflate(new Thickness(thickness));
   }

   /// <summary>
   /// Deflates the rectangle by a <see cref="Thickness"/>.
   /// </summary>
   /// <param name="thickness">The thickness.</param>
   /// <returns>The deflated rectangle.</returns>
   /// <remarks>The deflated rectangle size cannot be less than 0.</remarks>
   public Rect Deflate(Thickness thickness)
   {
      return new Rect(
         new Vector2(X + thickness.Left, Y + thickness.Top),
         Size.Deflate(thickness));
   }

   public Rect ReplaceWidth(double width)
   {
      return new Rect(X, Y, width, Height);
   }

   public Rect ReplaceHeight(double height)
   {
      return new Rect(X, Y, Width, height);
   }

   /// <summary>
   /// Returns a boolean indicating whether the given object is equal to this rectangle.
   /// </summary>
   /// <param name="obj">The object to compare against.</param>
   /// <returns>True if the object is equal to this rectangle; false otherwise.</returns>
   public override bool Equals(object obj)
   {
      if (obj is Rect)
      {
         var other = (Rect)obj;
         return Location == other.Location && Size == other.Size;
      }

      return false;
   }

   /// <summary>
   /// Returns the hash code for this instance.
   /// </summary>
   /// <returns>The hash code.</returns>
   public override int GetHashCode()
   {
      unchecked
      {
         int hash = 17;
         hash = (hash * 23) + X.GetHashCode();
         hash = (hash * 23) + Y.GetHashCode();
         hash = (hash * 23) + Width.GetHashCode();
         hash = (hash * 23) + Height.GetHashCode();
         return hash;
      }
   }

   /// <summary>
   /// Gets the intersection of two rectangles.
   /// </summary>
   /// <param name="rect">The other rectangle.</param>
   /// <returns>The intersection.</returns>
   public Rect Intersect(Rect rect)
   {
      var newLeft = (rect.X > X) ? rect.X : X;
      var newTop = (rect.Y > Y) ? rect.Y : Y;
      var newRight = (rect.Right < Right) ? rect.Right : Right;
      var newBottom = (rect.Bottom < Bottom) ? rect.Bottom : Bottom;

      if ((newRight > newLeft) && (newBottom > newTop))
      {
         return new Rect(newLeft, newTop, newRight - newLeft, newBottom - newTop);
      }
      else
      {
         return Empty;
      }
   }

   /// <summary>
   /// Determines whether a rectangle intersects with this rectangle.
   /// </summary>
   /// <param name="rect">The other rectangle.</param>
   /// <returns>
   /// True if the specified rectangle intersects with this one; otherwise false.
   /// </returns>
   public bool Intersects(Rect rect)
   {
      return (rect.X <= Right) && (X <= rect.Right) && (rect.Y <= Bottom) && (Y <= rect.Bottom);
   }

   public Rect Merge(Rect rect)
   {
      var points = GetPoints();
      points.AddRange(rect.GetPoints());
      return FromPoints(points);
   }

   public List<Vector2> GetPoints()
   {
      return new List<Vector2> { Location, BottomRight };
   }

   /// <summary>
   /// Returns the axis-aligned bounding box of a transformed rectangle.
   /// </summary>
   /// <param name="matrix">The transform.</param>
   /// <returns>The bounding box</returns>
   public Rect TransformToAABB(Matrix4x4 matrix)
   {
      var points = new[]
      {
         Vector2.TransformCoordinate(TopLeft, matrix),
         Vector2.TransformCoordinate(TopRight, matrix),
         Vector2.TransformCoordinate(BottomRight, matrix),
         Vector2.TransformCoordinate(BottomLeft, matrix)
      };

      var left = double.MaxValue;
      var right = double.MinValue;
      var top = double.MaxValue;
      var bottom = double.MinValue;

      foreach (var p in points)
      {
         if (p.X < left) left = p.X;
         if (p.X > right) right = p.X;
         if (p.Y < top) top = p.Y;
         if (p.Y > bottom) bottom = p.Y;
      }

      return new Rect(new Vector2(left, top), new Vector2(right, bottom));
   }
      
   /// <summary>
   /// Translates the rectangle by an offset.
   /// </summary>
   /// <param name="offset">The offset.</param>
   /// <returns>The translated rectangle.</returns>
   public Rect Translate(Vector2 offset)
   {
      return new Rect(Location + offset, Size);
   }

   /// <summary>
   /// Returns the string representation of the rectangle.
   /// </summary>
   /// <returns>The string representation of the rectangle.</returns>
   public override string ToString()
   {
      return string.Format(
         CultureInfo.InvariantCulture,
         "{0}, {1}, {2}, {3}",
         X,
         Y,
         Width,
         Height);
   }
      
   public static Rect FromPoints(IEnumerable<Vector2> inPoints)
   {
      ArgumentNullException.ThrowIfNull(inPoints);

      var points = inPoints as Vector2[] ?? inPoints.ToArray();
      
      if (points.Length == 0)
      {
         return Rect.Empty;
      }

      var minimum = new Vector2(float.MaxValue);
      var maximum = new Vector2(float.MinValue);

      for (int i = 0; i < points.Length; ++i)
      {
         Vector2.Min(ref minimum, ref points[i], out minimum);
         Vector2.Max(ref maximum, ref points[i], out maximum);
      }

      var rect = maximum - minimum;
      return new Rect(minimum.X, minimum.Y, rect.X, rect.Y);
   }
      
   public static Rect FromPoints(IEnumerable<Vector3> inPoints)
   {
      ArgumentNullException.ThrowIfNull(inPoints);

      var points = inPoints as Vector3[] ?? inPoints.ToArray();
      var minimum = new Vector3(double.MaxValue);
      var maximum = new Vector3(double.MinValue);

      for (int i = 0; i < points.Length; ++i)
      {
         Vector3.Min(ref minimum, ref points[i], out minimum);
         Vector3.Max(ref maximum, ref points[i], out maximum);
      }

      var rect = maximum - minimum;
      return new Rect(minimum.X, minimum.Y, rect.X, rect.Y);
   }

   public static Rect Parse(string values)
   {
      var parsedValues = values.Split(new string[] { " ", "," },
         StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
      if (parsedValues.Length < 4)
         throw new ArgumentException($"{values} must contain 4 parameters, but provide only {parsedValues.Length}");

      var rect = new Rect(
         double.Parse(parsedValues[0]),
         double.Parse(parsedValues[1]), 
      double.Parse(parsedValues[2]), 
      double.Parse(parsedValues[3]));
      return rect;
   }
}