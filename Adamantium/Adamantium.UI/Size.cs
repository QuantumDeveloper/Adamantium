using System;

namespace Adamantium.UI;

/// <summary>
/// Defines a size
/// </summary>
public struct Size:IEquatable<Size>
{
   /// <summary>
   /// Return new size with zero values
   /// </summary>
   public static readonly Size Zero = new Size();

   /// <summary>
   /// A size representing infinity.
   /// </summary>
   public static readonly Size Infinity = new Size(double.PositiveInfinity, double.PositiveInfinity);

   /// <summary>
   /// Size Width
   /// </summary>
   public Double Width { get; set; }
      
   /// <summary>
   /// Size Height
   /// </summary>
   public Double Height { get; set; }

   /// <summary>
   /// Initializes a new instance of the <see cref="Size"/> structure.
   /// </summary>
   /// <param name="width">width value</param>
   /// <param name="height">height value</param>
   public Size(double width, double height)
   {
      Width = width;
      Height = height;
   }

   /// <summary>
   /// Initializes a new instance of the <see cref="Size"/> structure.
   /// </summary>
   public Size(Size size)
   {
      Width = size.Width;
      Height = size.Height;
   }

   /// <summary>
   /// Adds 2 <see cref="Size"/>
   /// </summary>
   /// <param name="size1">size1</param>
   /// <param name="size2">size2</param>
   /// <returns></returns>
   public static Size operator +(Size size1, Size size2)
   {
      return new Size(size1.Width + size2.Width, size1.Height + size2.Height);
   }

   /// <summary>
   /// Subtracts 2 <see cref="Size"/>
   /// </summary>
   /// <param name="size1">size1</param>
   /// <param name="size2">size2</param>
   /// <returns></returns>
   public static Size operator -(Size size1, Size size2)
   {
      return new Size(size1.Width - size2.Width, size1.Height - size2.Height);
   }

   /// <summary>
   /// Multiply <see cref="Size"/> by a double value
   /// </summary>
   /// <param name="size">size</param>
   /// <param name="scale">scale factor</param>
   /// <returns>returns scaled Size</returns>
   public static Size operator *(Size size, Double scale)
   {
      return new Size(size.Width * scale, size.Height * scale);
   }

   /// <summary>
   /// Divide <see cref="Size"/> by a double value
   /// </summary>
   /// <param name="size">size</param>
   /// <param name="scale">scale factor</param>
   /// <returns>returns scaled Size</returns>
   public static Size operator /(Size size, Double scale)
   {
      return new Size(size.Width / scale, size.Height / scale);
   }

   /// <summary>
   /// Multiply <see cref="Size"/> by a <see cref="Vector2"/>
   /// </summary>
   /// <param name="size">size</param>
   /// <param name="scale">scale factor</param>
   /// <returns>returns scaled Size</returns>
   public static Size operator *(Size size, Vector2 scale)
   {
      return new Size(size.Width * scale.X, size.Height * scale.Y);
   }

   /// <summary>
   /// Divide <see cref="Size"/> by a <see cref="Vector2"/>
   /// </summary>
   /// <param name="size">size</param>
   /// <param name="scale">scale factor</param>
   /// <returns>returns scaled Size</returns>
   public static Size operator /(Size size, Vector2 scale)
   {
      return new Size(size.Width / scale.X, size.Height / scale.Y);
   }

   /// <summary>
   /// Constrains the size.
   /// </summary>
   /// <param name="constraint">The size to constrain to.</param>
   /// <returns>The constrained size.</returns>
   public Size Constrain(Size constraint)
   {
      return new Size(
         Math.Min(Width, constraint.Width),
         Math.Min(Height, constraint.Height));
   }

   /// <summary>
   /// Deflates the size by a <see cref="Thickness"/>.
   /// </summary>
   /// <param name="thickness">The thickness.</param>
   /// <returns>The deflated size.</returns>
   /// <remarks>The deflated size cannot be less than 0.</remarks>
   public Size Deflate(Thickness thickness)
   {
      return new Size(
         Math.Max(0, Width - thickness.Left - thickness.Right),
         Math.Max(0, Height - thickness.Top - thickness.Bottom));
   }

   /// <summary>
   /// Inflates the size by a <see cref="Thickness"/>.
   /// </summary>
   /// <param name="thickness">The thickness.</param>
   /// <returns>The deflated size.</returns>
   /// <remarks>The deflated size cannot be less than 0.</remarks>
   public Size Inflate(Thickness thickness)
   {
      return new Size(
         Math.Max(0, Width + thickness.Left + thickness.Right),
         Math.Max(0, Height + thickness.Top + thickness.Bottom));
   }

   public static bool IsZero(Size size)
   {
      return size.Width == 0 && size.Height == 0;
   }

   /// <summary>
   /// Converts Size2D to Point
   /// </summary>
   /// <param name="size"></param>
   /// <returns></returns>
   public static implicit operator Vector2(Size size)
   {
      return new Vector2(size.Width, size.Height);
   }

   /// <summary>
   /// Comparison operator for 2 <see cref="Size"/>
   /// </summary>
   /// <returns>returns true if both Size are equals</returns>
   public static bool operator ==(Size size1, Size size2)
   {
      return size1.Width == size2.Width && size1.Height == size2.Height;
   }

   /// <summary>
   /// Comparison operator for 2 <see cref="Size"/>
   /// </summary>
   /// <returns>returns true if one of Size parameters NOT equals</returns>
   public static bool operator !=(Size size1, Size size2)
   {
      return !(size1 == size2);
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
      if (obj is Size)
      {
         var other = (Size)obj;
         return other == this;
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
         hash = (hash * 98) ^ Width.GetHashCode();
         hash = (hash * 98) ^ Height.GetHashCode();
         return hash;
      }
   }

   public bool Equals(Size other)
   {
      return other == this;
   }

   /// <summary>
   /// Returns the fully qualified type name of this instance.
   /// </summary>
   /// <returns>
   /// A <see cref="T:System.String"/> containing a fully qualified type name.
   /// </returns>
   /// <filterpriority>2</filterpriority>
   public override string ToString() => "Width: " + Width + " Height: " + Height;
}