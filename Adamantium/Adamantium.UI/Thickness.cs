using System;

namespace Adamantium.UI
{
   public struct Thickness
   {
      public Double Left { get; set; }
      public Double Top { get; set; }
      public Double Right { get; set; }
      public Double Bottom { get; set; }

      public Thickness(Double left, Double top, Double right, Double bottom)
      {
         Left = left;
         Top = top;
         Right = right;
         Bottom = bottom;
      }

      public Thickness(Double uniformValue)
      {
         Left = Top = Right = Bottom = uniformValue;
      }

      /// <summary>
      /// Adds two Thicknesses.
      /// </summary>
      /// <param name="a">The first thickness.</param>
      /// <param name="b">The second thickness.</param>
      /// <returns>The equality.</returns>
      public static Thickness operator +(Thickness a, Thickness b)
      {
         return new Thickness(
             a.Left + b.Left,
             a.Top + b.Top,
             a.Right + b.Right,
             a.Bottom + b.Bottom);
      }

      public override string ToString() => $"Left: {Left}, Top: {Top}, Right {Right}, Bottom {Bottom}";

   }
}
