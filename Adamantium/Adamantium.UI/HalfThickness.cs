using System;

namespace Adamantium.UI;

public struct HalfThickness
{
   public Double TopLeft { get; set; }

   public Double BottomRight { get; set; }

   public HalfThickness(Double left, Double right)
   {
      TopLeft = left;
      BottomRight = right;
   }

   public HalfThickness(Double uniformValue)
   {
      TopLeft = BottomRight = uniformValue;
   }

   /// <summary>
   /// Adds two <see cref="HalfThickness"/>.
   /// </summary>
   /// <param name="a">The first <see cref="HalfThickness"/>.</param>
   /// <param name="b">The second <see cref="HalfThickness"/>.</param>
   /// <returns>The equality.</returns>
   public static HalfThickness operator +(HalfThickness a, HalfThickness b)
   {
      return new HalfThickness(a.TopLeft + b.TopLeft, a.BottomRight + b.BottomRight);
   }

   public static double operator +(double a, HalfThickness b)
   {
      return a + b.TopLeft + b.BottomRight;
   }

   public static double operator +(HalfThickness a, double b)
   {
      return b + a.TopLeft + a.BottomRight;
   }

   public static double operator -(double a, HalfThickness b)
   {
      return a - b.TopLeft - b.BottomRight;
   }

   public static double operator -(HalfThickness a, double b)
   {
      return b - a.TopLeft - a.BottomRight;
   }

   public override string ToString() => $"Left: {TopLeft}, Right {BottomRight}";
}