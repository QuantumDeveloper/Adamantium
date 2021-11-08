using System.Collections.Generic;

namespace Adamantium.UI.Media
{
   public class PathGeometry : Geometry
   {
      public override Rect Bounds { get; }

      public override Geometry Clone()
      {
         return null;
      }

      public PathGeometry()
      {
         geometries = new List<Geometry>();
      }

      private List<Geometry> geometries;
   }
}
