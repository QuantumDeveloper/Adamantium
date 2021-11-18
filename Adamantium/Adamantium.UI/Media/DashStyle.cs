using System;
using System.Collections.Generic;

namespace Adamantium.UI.Media
{
   public class DashStyle
   {
      public IReadOnlyList<Double> Dashes { get; }

      public double Offset { get; }

      public DashStyle(IReadOnlyList<Double> dashes = null, double offset = 0.0)
      {
         Dashes = dashes;
         Offset = offset;
      }


      private static DashStyle dash;
      private static DashStyle dashDotDot;
      private static DashStyle dashDot;
      private static DashStyle dot;

      public static DashStyle Dash
      {
         get
         {
            if (dashDotDot == null)
            {
               dash = new DashStyle(new double[] { 2, 2, 0, 2, 0, 2 }, 1);
            }
            return dash;
         }
      }

      public static DashStyle DashDotDot => dashDotDot ??= new DashStyle(new double[] { 2, 2, 0, 2, 0, 2 }, 1);

      public static DashStyle DashDot => dashDot ??= new DashStyle(new double[] { 2, 2, 0, 2 }, 1);

      public static DashStyle Dot => dot ??= new DashStyle(new double[] { 0, 2 });
   }
}
