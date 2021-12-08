using System;
using System.Collections.Generic;
using Adamantium.Core.Collections;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public class Pen
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
   }
}
