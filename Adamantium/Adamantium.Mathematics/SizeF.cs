using System;
using System.Runtime.InteropServices;

namespace Adamantium.Mathematics
{
   /// <summary>
   /// Structure using the same layout than <see cref="SizeF"/>.
   /// </summary>
   [StructLayout(LayoutKind.Sequential)]
   public struct SizeF: IEquatable<SizeF>
   {
      /// <summary>
      /// A zero size with (width, height) = (0,0)
      /// </summary>
      public static readonly SizeF Zero = new SizeF(0, 0);

      /// <summary>
      /// A zero size with (width, height) = (0,0)
      /// </summary>
      public static readonly SizeF Empty = Zero;

      /// <summary>
      /// Initializes a new instance of the <see cref="SizeF"/> struct.
      /// </summary>
      /// <param name="width">The x.</param>
      /// <param name="height">The y.</param>
      public SizeF(float width, float height)
      {
         Width = width;
         Height = height;
      }

      /// <summary>
      /// Width.
      /// </summary>
      public float Width;

      /// <summary>
      /// Height.
      /// </summary>
      public float Height;

      /// <summary>
      /// Determines whether the specified <see cref="System.Object"/> is equal to this instance.
      /// </summary>
      /// <param name="other">The <see cref="System.Object"/> to compare with this instance.</param>
      /// <returns>
      ///   <c>true</c> if the specified <see cref="System.Object"/> is equal to this instance; otherwise, <c>false</c>.
      /// </returns>
      public bool Equals(SizeF other)
      {
         return MathHelper.NearEqual(other.Width, Width) && MathHelper.NearEqual(other.Height, Height);
      }

      /// <inheritdoc/>
      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         if (obj.GetType() != typeof(SizeF)) return false;
         return Equals((SizeF)obj);
      }

      /// <inheritdoc/>
      public override int GetHashCode()
      {
         unchecked
         {
            return (Width.GetHashCode() * 397) ^ Height.GetHashCode();
         }
      }

      /// <summary>
      /// Implements the operator ==.
      /// </summary>
      /// <param name="left">The left.</param>
      /// <param name="right">The right.</param>
      /// <returns>
      /// The result of the operator.
      /// </returns>
      public static bool operator ==(SizeF left, SizeF right)
      {
         return left.Equals(right);
      }

      /// <summary>
      /// Implements the operator !=.
      /// </summary>
      /// <param name="left">The left.</param>
      /// <param name="right">The right.</param>
      /// <returns>
      /// The result of the operator.
      /// </returns>
      public static bool operator !=(SizeF left, SizeF right)
      {
         return !left.Equals(right);
      }

      public override string ToString()
      {
         return $"({Width},{Height})";
      }
   }
}
