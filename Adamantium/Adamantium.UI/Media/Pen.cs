using System;
using Adamantium.Mathematics;

namespace Adamantium.UI.Media
{
   public class Pen
   {
      public Brush Brush { get; }

      public Double Thickness { get; }

      public DashStyle DashStyle { get; }

      public PenLineCap DashCap { get; set; }

      public PenLineCap StartLineCap { get; set; }

      public PenLineCap EndLineCap { get; set; }

      public PenLineJoin PenLineJoin { get; set; }

      public Pen(Vector4D brush, Double thicness = 1.0, DashStyle dashStyle = null, PenLineCap dashCap = PenLineCap.Flat,
         PenLineCap startLineCap = PenLineCap.Flat,
         PenLineCap endLineCap = PenLineCap.Flat, PenLineJoin penLineJoin = PenLineJoin.Miter)
      {
         Brush = new SolidColorBrush(Colors.Transparent);
         Thickness = thicness;
         DashStyle = dashStyle;
         DashCap = dashCap;
         StartLineCap = startLineCap;
         EndLineCap = endLineCap;
         PenLineJoin = penLineJoin;
      }

      public Pen(Brush brush, Double thicness = 1.0, DashStyle dashStyle = null, PenLineCap dashCap = PenLineCap.Flat, PenLineCap startLineCap = PenLineCap.Flat,
         PenLineCap endLineCap = PenLineCap.Flat, PenLineJoin penLineJoin = PenLineJoin.Miter)
      {
         Brush = brush;
         Thickness = thicness;
         DashStyle = dashStyle;
         DashCap = dashCap;
         StartLineCap = startLineCap;
         EndLineCap = endLineCap;
         PenLineJoin = penLineJoin;
      }
   }

   
}
