using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Adamantium.Mathematics;

public struct Thickness
{
   public Double Left { get; set; }
   public Double Top { get; set; }
   public Double Right { get; set; }
   public Double Bottom { get; set; }
   
   public Thickness(IEnumerable<double> values)
   {
      var lst = values as List<double> ?? values.ToList();

      if (lst.Count < 4) throw new ArgumentOutOfRangeException($"Arguments count for Corner radius should be 4, but provided {lst.Count}");

      Left = lst[0];
      Top = lst[1];
      Right = lst[2];
      Bottom = lst[3];
   }
   
   /// <summary>Two values read the way every XAML dialect reads them: HORIZONTAL, then VERTICAL.</summary>
   public Thickness(Double horizontal, Double vertical)
   {
      Left = Right = horizontal;
      Top = Bottom = vertical;
   }

   public Thickness(Double left, Double top, Double right, Double bottom)
   {
      Left = Double.IsNaN(left) ? 0 : left;
      Top = Double.IsNaN(top) ? 0 : top;
      Right = Double.IsNaN(right) ? 0 : right;
      Bottom = Double.IsNaN(bottom) ? 0 : bottom;
   }

   public Thickness(Double uniformValue)
   {
      Left = Top = Right = Bottom = uniformValue;
   }

   /// <summary>True when all four sides are equal (a single value describes the whole thickness).</summary>
   public bool IsUniform => Left == Top && Top == Right && Right == Bottom;

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
   
   public static Thickness operator -(Thickness a, Thickness b)
   {
      return new Thickness(
         a.Left - b.Left,
         a.Top - b.Top,
         a.Right - b.Right,
         a.Bottom - b.Bottom);
   }
   
   public static Thickness operator *(Thickness a, double value)
   {
      return new Thickness(
         a.Left * value,
         a.Top * value,
         a.Right * value,
         a.Bottom * value);
   }
   
   public static Thickness operator /(Thickness a, double value)
   {
      return new Thickness(
         a.Left / value,
         a.Top / value,
         a.Right / value,
         a.Bottom / value);
   }

   public override string ToString() => $"Left: {Left}, Top: {Top}, Right {Right}, Bottom {Bottom}";

}