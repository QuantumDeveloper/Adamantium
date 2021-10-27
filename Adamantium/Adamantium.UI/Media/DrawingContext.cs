using System;
using System.Collections.Generic;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media.Imaging;

namespace Adamantium.UI.Media
{
   public class DrawingContext
   {
      internal GraphicsDevice GraphicsDevice { get; set; }
      internal Dictionary<IUIComponent, ShapePresentation> VisualPresentations;

      internal DrawingContext(GraphicsDevice d3dDevice)
      {
         GraphicsDevice = d3dDevice;
         VisualPresentations = new Dictionary<IUIComponent, ShapePresentation>();
      }

      public void BeginDraw(IUIComponent visualComponent)
      {
         if (VisualPresentations.ContainsKey(visualComponent))
         {
            var presentation = VisualPresentations[visualComponent];
            presentation.DisposeAndClearItems();
            presentation.IsSealed = false;
         }
         else
         {
            VisualPresentations[visualComponent] = new ShapePresentation();
         }
      }

      public void EndDraw(IUIComponent visualComponent)
      {
         VisualPresentations[visualComponent].IsSealed = true;
      }

      public void DrawRectangle(IUIComponent visualComponent, Brush brush, Rect rect, Pen pen = null, double radiusX = 0.0, double radiusY = 0.0)
      {
         RectangleGeometry rectangle = new RectangleGeometry(rect, radiusX, radiusY);
         StrokeGeometry stroke = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            stroke = new StrokeGeometry(pen, rectangle);
         }

         ShapePresentation shapePresentation = null;
         VisualPresentations.TryGetValue(visualComponent, out shapePresentation);
         shapePresentation?.AddItem(new PresentationItem(this, null, rectangle, stroke, brush, pen?.Brush));

      }

      public void DrawRectangle(IUIComponent visualComponent, Brush brush, Rect rect, Thickness corners,Pen pen = null)
      {
         RectangleGeometry rectangle = new RectangleGeometry(rect, corners);
         StrokeGeometry stroke = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            stroke = new StrokeGeometry(pen, rectangle);
         }

         ShapePresentation shapePresentation = null;
         VisualPresentations.TryGetValue(visualComponent, out shapePresentation);
         shapePresentation?.AddItem(new PresentationItem(this, null, rectangle, stroke, brush, pen?.Brush));

      }

      public void DrawGeometry(IUIComponent visualComponent, Brush brush, Pen pen, Geometry geometry)
      {
         ShapePresentation shapePresentation = null;
         VisualPresentations.TryGetValue(visualComponent, out shapePresentation);
         StrokeGeometry strokeGeometry = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            strokeGeometry = new StrokeGeometry(pen, geometry);
         }
         shapePresentation = new ShapePresentation();
         shapePresentation.AddItem(new PresentationItem(this, null, geometry, strokeGeometry, brush, pen?.Brush));
         if (!VisualPresentations.ContainsKey(visualComponent))
         {
            VisualPresentations.Add(visualComponent, shapePresentation);
         }
         else
         {
            VisualPresentations[visualComponent] = shapePresentation;
         }

      }

      public void DrawImage(IUIComponent visualComponent, BitmapSource bitmap, Brush filter, Rect destinationRect, Double radiusX,
         Double radiusY)
      {
         ShapePresentation shapePresentation = new ShapePresentation();
         var geometry = new RectangleGeometry(destinationRect, radiusX, radiusY);
         shapePresentation.AddItem(new PresentationItem(this, bitmap, geometry, null, filter, null));
         if (!VisualPresentations.ContainsKey(visualComponent))
         {
            VisualPresentations.Add(visualComponent, shapePresentation);
         }
         else
         {
            VisualPresentations[visualComponent] = shapePresentation;
         }
      }

      public void PushTexture(IUIComponent visualComponent, BitmapSource bitmap)
      {
         ShapePresentation shapePresentation = null;

         if (VisualPresentations.TryGetValue(visualComponent, out shapePresentation))
         {
            if (shapePresentation[0].HasTexture)
            {
               var textured = shapePresentation[0];
               textured.Texture = bitmap.DXTexture;
            }
         }
      }

      public void PushTexture(IUIComponent visualComponent, Texture bitmap)
      {
         ShapePresentation shapePresentation = null;

         if (VisualPresentations.TryGetValue(visualComponent, out shapePresentation))
         {
            if (shapePresentation[0].HasTexture)
            {
               var textured = shapePresentation[0];
               textured.Texture = bitmap;
            }
         }
      }

      public void DrawEmptyRectangle(IUIComponent visualComponent, Brush brush, Rect rect, Thickness borderThickness, Thickness cornerRadius)
      {
         
      }

      public void DrawFilledRectangle(IUIComponent visualComponent, Brush brush, Rect rect, Thickness cornerRadius)
      {

      }

   }
}
