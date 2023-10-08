using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Adamantium.Engine.Graphics;

namespace Adamantium.UI;

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
   
   public static Thickness Parse(string value)
   {
      var values = value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

      if (values.Length == 1)
      {
         return new Thickness(double.Parse(values[0], CultureInfo.InvariantCulture));
      }

      var list = new List<double>();
      foreach (var v in values)
      {
         list.Add(double.Parse(v, CultureInfo.InvariantCulture));
      }

      return new Thickness(list);
   }

   public override string ToString() => $"Left: {Left}, Top: {Top}, Right {Right}, Bottom {Bottom}";

}