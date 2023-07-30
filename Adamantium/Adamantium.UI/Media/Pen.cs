using System;
using System.Collections.Generic;
using Adamantium.Core.Collections;

namespace Adamantium.UI.Media;

public class Pen : IEquatable<Pen>
{
   public Brush Brush { get; }

   public Double Thickness { get; }

   public Double DashOffset { get; }
      
   public AdamantiumCollection<Double> DashStrokeArray { get; }

   public PenLineCap StartLineCap { get; set; }

   public PenLineCap EndLineCap { get; set; }

   public PenLineJoin PenLineJoin { get; set; }
      
   public Pen(
      Brush brush, 
      Double thickness = 1.0, 
      Double dashOffset = 0,
      IEnumerable<Double> dashStrokeArray = null,
      PenLineCap startLineCap = PenLineCap.Flat,
      PenLineCap endLineCap = PenLineCap.Flat, 
      PenLineJoin penLineJoin = PenLineJoin.Miter)
   {
      Brush = brush;
      DashStrokeArray = new AdamantiumCollection<double>(dashStrokeArray);
      DashOffset = dashOffset;
      Thickness = thickness;
      StartLineCap = startLineCap;
      EndLineCap = endLineCap;
      PenLineJoin = penLineJoin;
   }

   public bool Equals(Pen other)
   {
      if (ReferenceEquals(null, other)) return false;
      if (ReferenceEquals(this, other)) return true;
      return Equals(Brush, other.Brush) && Thickness.Equals(other.Thickness) && DashOffset.Equals(other.DashOffset) &&
             Equals(DashStrokeArray, other.DashStrokeArray) && StartLineCap == other.StartLineCap &&
             EndLineCap == other.EndLineCap && PenLineJoin == other.PenLineJoin;
   }

   public override bool Equals(object obj)
   {
      if (ReferenceEquals(null, obj)) return false;
      if (ReferenceEquals(this, obj)) return true;
      if (obj.GetType() != this.GetType()) return false;
      return Equals((Pen)obj);
   }

   public override int GetHashCode()
   {
      return HashCode.Combine(Brush, Thickness, DashOffset, DashStrokeArray, (int)StartLineCap, (int)EndLineCap, (int)PenLineJoin);
   }
}