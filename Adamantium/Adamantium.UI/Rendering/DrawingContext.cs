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

   IDrawingSession IDrawingSession.DrawBorder(Brush background, Rect destinationRect, CornerRadius corners,
      Brush borderBrush, Thickness borderThickness)
   {
      var hasBorder = borderBrush.IsVisible()
         && (borderThickness.Left > 0 || borderThickness.Top > 0 || borderThickness.Right > 0 || borderThickness.Bottom > 0);
      // No border to draw -> this IS a plain rect, and saying so keeps one shape one payload (a frame payload with a
      // zero border would take the frame path in the shader for nothing).
      if (!hasBorder)
      {
         return ((IDrawingSession)this).DrawRectangle(background, destinationRect, corners);
      }

      var payload = new RectanglePayload(background, destinationRect, corners, borderBrush, borderThickness)
      {
         AntiAlias = _currentComponent?.UseAnalyticAA ?? true
      };
      CreateCommand(payload);
      return this;
   }

   IDrawingSession IDrawingSession.DrawRectangle(Brush brush, Rect destinationRect, CornerRadius corners, Pen pen)
   {
      // Nothing VISIBLE to fill and no border to stroke -> draw nothing, and don't create a render unit (with its GPU
      // buffers + AA fringe) for it. A null OR a fully-transparent brush both paint nothing: Border/Panel/TextBlock all
      // DEFAULT their Background to Brushes.Transparent, so without folding transparent in here every rest-state
      // ListBoxItem (and each item's text background) still piled up an invisible fill unit + fringe - the ListBox FPS
      // drop. Hit-testing is bounds-based, so nothing depends on the invisible draw.
      if (!brush.IsVisible() && pen == null)
         return this;

      // The payload keeps the LIVE brush/pen and reads their immutable snapshots (Brush.Snapshot): a brush is animatable,
      // and a paint change repaints by RE-BAKING this payload rather than re-recording the element - so a payload holding a
      // snapshot taken here would pin the colour forever.
      var payload = new RectanglePayload(
      brush,
      destinationRect,
      corners,
      pen)
      {
         // Whether this shape wants a fringe is the COMPONENT's answer, and here is the only place the component is
         // known - by record time it is gone.
         AntiAlias = _currentComponent?.UseAnalyticAA ?? true
      };
      CreateCommand(payload);

      return this;
   }

   IDrawingSession IDrawingSession.DrawEllipse(Rect destinationRect,
      Brush brush,
      double startAngle,
      double sweepAngle,
      EllipseType ellipseType,
      Pen pen,
      double ringThickness)
   {
      var payload = new EllipsePayload(
         brush,
         destinationRect,
         startAngle,
         sweepAngle,
         ellipseType,
         pen
      )
      {
         RingThickness = ringThickness
      };
      CreateCommand(payload);
      return this;
   }

   IDrawingSession IDrawingSession.DrawRegularPolygon(Brush brush, Rect destinationRect, int corners, Pen pen, double ringThickness, double startAngle)
   {
      // Same invisible-draw skip as DrawRectangle: nothing to fill and no stroke records nothing.
      if (!brush.IsVisible() && pen == null) return this;

      var payload = new RegularPolygonPayload(brush, destinationRect, corners, pen)
      {
         RingThickness = ringThickness,
         StartAngle = startAngle
      };
      CreateCommand(payload);
      return this;
   }

   IDrawingSession IDrawingSession.DrawGeometry(Brush brush, Geometry geometry, Pen pen)
   {
      // Same invisible-draw skip as DrawRectangle: a transparent/null fill with no stroke records nothing.
      if (!brush.IsVisible() && pen == null) return this;
      var payload = new GeometryPayload(brush, geometry, pen);
      CreateCommand(payload);
      return this;
   }

   IDrawingSession IDrawingSession.DrawGeometry(Brush brush, Geometry geometry, Pen pen, Matrix4x4F transform)
   {
      if (!brush.IsVisible() && pen == null) return this;
      CreateCommand(new GeometryPayload(brush, geometry, pen, transform));
      return this;
   }

   IDrawingSession IDrawingSession.DrawImage(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners)
   {
      var payload = new ImagePayload(filter, image, destinationRect, corners);
      CreateCommand(payload);
      return this;
   }

   IDrawingSession IDrawingSession.DrawImage(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners, Rect sourceUv)
   {
      var payload = new ImagePayload(filter, image, destinationRect, corners, sourceUv);
      CreateCommand(payload);
      return this;
   }

   IDrawingSession IDrawingSession.DrawImageFrame(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners, int frameLayer)
   {
      var payload = new ImagePayload(filter, image, destinationRect, corners, null, frameLayer);
      CreateCommand(payload);
      return this;
   }

   public IDrawingSession PushImage(ImageSource image)
   {
      throw new NotImplementedException();
   }
   
   IDrawingSession IDrawingSession.DrawText(TextRenderingParameters renderingParameters, Size desiredSize, TextLayout textLayout, Brush foreground, Brush background, Brush stroke)
   {
      // Only emit the text's background rect when it's actually visible. Every TextBlock passed its Background here
      // (default Brushes.Transparent), so each list item's text was creating an invisible fill unit + fringe on top of
      // the (now batched) glyphs - a big slice of a long list's cost. The glyph draw itself is always recorded below.
      if (background.IsVisible())
         CreateCommand(new RectanglePayload(background, new Rect(desiredSize), CornerRadius.Empty, null));
      var payload = new TextPayload(renderingParameters, desiredSize, textLayout, foreground, background, stroke);
      CreateCommand(payload);
      return this;
   }

   IDrawingSession IDrawingSession.DrawText(TextRenderingParameters renderingParameters, Size desiredSize, TextLayout textLayout, Brush foreground, Brush background, Brush stroke, Matrix4x4F transform)
   {
      if (background.IsVisible())
         CreateCommand(new RectanglePayload(background, new Rect(desiredSize), CornerRadius.Empty, null));
      
      CreateCommand(new TextPayload(renderingParameters, desiredSize, textLayout, foreground, background, stroke, transform));
      return this;
   }

   private RenderData GetRenderDataFromComponent()
   {
      var renderData = new RenderData(GetEffectiveOpacity(),
         _currentComponent.WorldTransform,
         _currentComponent.ClipToBounds,
         _currentComponent.ClipRectangle);
      // Baked HERE, on the record thread: the aura/shadow objects are edited from the loop thread, so the renderer gets
      // values, never the live objects.
      renderData.Halo = HaloBake.From(_currentComponent.Aura, _currentComponent.Shadow);
      renderData.LivingHalo = HaloBake.Living(_currentComponent.Aura, GradientBake.PackPalette);
      //renderData.CustomEffect = _currentComponent.Effect TODO: implement custom effects for controls
      return renderData;
   }

   // Opacity composites DOWN the visual tree (WPF semantics): a control's effective opacity is its own Opacity times
   // every ancestor's. Without this a COMPOSITE control ignores its own Opacity - e.g. a Button draws nothing itself,
   // its pixels come from its template's Border child (Opacity 1), so Button.Opacity never reached those draw commands.
   // SelfOpacity is the NON-composited counterpart: it fades only THIS element's own draws, so it is folded in here but
   // the ancestor walk multiplies each parent's Opacity ONLY, never their SelfOpacity - so it never reaches descendants.
   private float GetEffectiveOpacity()
   {
      var opacity = _currentComponent.Opacity * _currentComponent.SelfOpacity;
      for (var parent = _currentComponent.VisualParent; parent != null; parent = parent.VisualParent)
         opacity *= parent.Opacity;
      return (float)opacity;
   }
}