using System;

namespace Adamantium.UI.Media;

public sealed class StreamGeometry : Geometry
{
   private Rect bounds;
   private StreamGeometryContext context;

   public StreamGeometry()
   {
      context = new StreamGeometryContext();
   }

   public override Rect Bounds => bounds;

   public IFigureSegments Open()
   {
      context = new StreamGeometryContext();
      return context;
   }

   public override Geometry Clone()
   {
      throw new NotImplementedException();
   }

   public override void RecalculateBounds()
   {
      bounds = Rect.FromPoints(context.GetPoints());
   }

   protected internal override void ProcessGeometryCore()
   {
      context.ProcessFigure();
   }
}