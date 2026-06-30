using System;
using System.Collections.Generic;
using Adamantium.Graphics.Fonts;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.ProceduralGeometry.Shapes;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Rendering.Payloads;

namespace Adamantium.UI.Rendering;

public class DrawingContext : IDrawingContext, IDrawingContextInternal, IDrawingSession
{
   private IUIComponent _currentComponent;
   private uint _currentIndex;
   
   private readonly List<DrawCommand> drawCommands = new List<DrawCommand>();

   internal DrawingContext()
   {
   }

   protected void CreateCommand(object payload)
   {
      var command = new DrawCommand(
         _currentComponent, 
         _currentComponent.RenderId, 
         payload, 
         GetRenderDataFromComponent());
      drawCommands.Add(command);
   }

   public void Clear()
   {
      drawCommands.Clear();
   }

   public IReadOnlyList<IDrawCommand> GetDrawCommands()
   {
      return drawCommands;
   }

   public IDrawingSession ForControl(IUIComponent component)
   {
      _currentComponent = component;
      return this;
   }

   IDrawingSession IDrawingSession.DrawLine(Vector2 start, Vector2 end, Pen pen)
   {
      var payload = new LinePayload(start, end, pen);
      CreateCommand(payload);
      return this;
   }

   IDrawingSession IDrawingSession.DrawRectangle(Brush brush, Rect destinationRect, Pen pen)
   {
      return ((IDrawingSession)this).DrawRectangle(brush, destinationRect, CornerRadius.Empty, pen);
   }

   IDrawingSession IDrawingSession.DrawRectangle(Brush brush, Rect destinationRect, CornerRadius corners, Pen pen)
   {
      // Nothing to fill and no border to stroke -> draw nothing, and don't create a render unit (with its GPU buffers)
      // for it. This is what lets a ListBoxItem's null rest background cost zero GPU resources per item, so a long
      // virtualized list (especially a wrap panel, which shows many rows at once) doesn't pile up fill units + AA fringes.
      if (brush == null && pen == null) return this;

      var payload = new RectanglePayload(
      brush,
      destinationRect,
      corners,
      pen);
      CreateCommand(payload);

      return this;
   }

   IDrawingSession IDrawingSession.DrawEllipse(Rect destinationRect, 
      Brush brush, 
      double startAngle, 
      double sweepAngle,
      EllipseType ellipseType, 
      Pen pen)
   {
      var payload = new EllipsePayload(
         brush,
         destinationRect,
         startAngle,
         sweepAngle,
         ellipseType,
         pen
      );
      CreateCommand(payload);
      return this;
   }

   IDrawingSession IDrawingSession.DrawGeometry(Brush brush, Geometry geometry, Pen pen)
   {
      var payload = new GeometryPayload(brush, geometry, pen);
      CreateCommand(payload);
      return this;
   }

   IDrawingSession IDrawingSession.DrawImage(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners)
   {
      var payload = new ImagePayload(filter, image, destinationRect, corners);
      CreateCommand(payload);
      return this;
   }

   public IDrawingSession PushImage(ImageSource image)
   {
      throw new NotImplementedException();
   }
   
   IDrawingSession IDrawingSession.DrawText(TextRenderingParameters renderingParameters, Size desiredSize, TextLayout textLayout, Brush foreground, Brush background, Brush stroke)
   {
      var rectPayload = new RectanglePayload(background, new Rect(desiredSize), CornerRadius.Empty, null);
      CreateCommand(rectPayload);
      var payload = new TextPayload(renderingParameters, desiredSize, textLayout, foreground, background, stroke);
      CreateCommand(payload);
      return this;
   }

   private RenderData GetRenderDataFromComponent()
   {
      var renderData = new RenderData(GetEffectiveOpacity(),
         _currentComponent.WorldTransform,
         _currentComponent.ClipToBounds,
         _currentComponent.ClipRectangle);
      //renderData.CustomEffect = _currentComponent.Effect TODO: implement custom effects for controls
      return renderData;
   }

   // Opacity composites DOWN the visual tree (WPF semantics): a control's effective opacity is its own Opacity times
   // every ancestor's. Without this a COMPOSITE control ignores its own Opacity - e.g. a Button draws nothing itself,
   // its pixels come from its template's Border child (Opacity 1), so Button.Opacity never reached those draw commands.
   private float GetEffectiveOpacity()
   {
      var opacity = _currentComponent.Opacity;
      for (var parent = _currentComponent.VisualParent; parent != null; parent = parent.VisualParent)
         opacity *= parent.Opacity;
      return (float)opacity;
   }
}