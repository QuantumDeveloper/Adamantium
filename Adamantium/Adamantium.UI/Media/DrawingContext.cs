using System;
using System.Collections.Generic;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media.Imaging;
using Adamantium.UI.Rendering;

namespace Adamantium.UI.Media
{
   public class DrawingContext
   {
      internal GraphicsDevice GraphicsDevice { get; }
      
      internal Dictionary<IUIComponent, UIPresentationContainer> VisualPresentations;

      internal DrawingContext(GraphicsDevice d3dDevice)
      {
         GraphicsDevice = d3dDevice;
         VisualPresentations = new Dictionary<IUIComponent, UIPresentationContainer>();
      }

      public void BeginDraw(IUIComponent visualComponent)
      {
         if (VisualPresentations.ContainsKey(visualComponent))
         {
            var presentation = VisualPresentations[visualComponent];
            presentation.DisposeAndClearItems();
         }
         else
         {
            VisualPresentations[visualComponent] = new UIPresentationContainer();
         }
      }

      public void EndDraw(IUIComponent visualComponent)
      {
         //VisualPresentations[visualComponent].IsSealed = true;
      }

      public void DrawRectangle(IUIComponent visualComponent, Brush brush, Rect rect, Pen pen = null, double radiusX = 0.0, double radiusY = 0.0)
      {
         RectangleGeometry rectangle = new RectangleGeometry(rect, radiusX, radiusY);
         StrokeGeometry stroke = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            stroke = new StrokeGeometry(pen, rectangle);
         }

         VisualPresentations.TryGetValue(visualComponent, out var uiPresentationContainer);
         var uiRenderer = UIComponentRenderer.Create(GraphicsDevice, rectangle.Mesh, brush);
         var strokeRenderer = UIComponentRenderer.Create(GraphicsDevice, stroke?.Mesh, pen?.Brush);
         var presentationItem = new UIPresentationItem();
         presentationItem.GeometryRenderer = uiRenderer;
         presentationItem.StrokeRenderer = strokeRenderer;
         uiPresentationContainer?.AddItem(presentationItem);

      }

      public void DrawRectangle(IUIComponent visualComponent, Brush brush, Rect rect, Thickness corners, Pen pen = null)
      {
         RectangleGeometry rectangle = new RectangleGeometry(rect, corners);
         StrokeGeometry stroke = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            stroke = new StrokeGeometry(pen, rectangle);
         }

         VisualPresentations.TryGetValue(visualComponent, out var uiPresentationContainer);
         var uiRenderer = UIComponentRenderer.Create(GraphicsDevice, rectangle.Mesh, brush);
         var strokeRenderer = UIComponentRenderer.Create(GraphicsDevice, stroke?.Mesh, pen?.Brush);
         var presentationItem = new UIPresentationItem();
         presentationItem.GeometryRenderer = uiRenderer;
         presentationItem.StrokeRenderer = strokeRenderer;
         uiPresentationContainer?.AddItem(presentationItem);
      }

      public void DrawGeometry(IUIComponent visualComponent, Brush brush, Pen pen, Geometry geometry)
      {
         VisualPresentations.TryGetValue(visualComponent, out var uiPresentation);
         StrokeGeometry strokeGeometry = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            strokeGeometry = new StrokeGeometry(pen, geometry);
         }

         uiPresentation = new UIPresentationContainer();
         var uiRenderer = UIComponentRenderer.Create(GraphicsDevice, geometry.Mesh, brush);
         var strokeRenderer = UIComponentRenderer.Create(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
         var presentationItem = new UIPresentationItem();
         presentationItem.GeometryRenderer = uiRenderer;
         presentationItem.StrokeRenderer = strokeRenderer;
         uiPresentation.AddItem(presentationItem);
         if (!VisualPresentations.ContainsKey(visualComponent))
         {
            VisualPresentations.Add(visualComponent, uiPresentation);
         }
         else
         {
            VisualPresentations[visualComponent] = uiPresentation;
         }

      }

      public void DrawImage(IUIComponent visualComponent, BitmapSource bitmap, Brush filter, Rect destinationRect, Double radiusX,
         Double radiusY)
      {
         var geometry = new RectangleGeometry(destinationRect, radiusX, radiusY);
         
         var uiPresentation = new UIPresentationContainer();
         var uiRenderer = UIComponentRenderer.Create(GraphicsDevice, geometry.Mesh, filter);
         var presentationItem = new UIPresentationItem();
         presentationItem.GeometryRenderer = uiRenderer;
         uiPresentation.AddItem(presentationItem);

         if (!VisualPresentations.ContainsKey(visualComponent))
         {
            VisualPresentations.Add(visualComponent, uiPresentation);
         }
         else
         {
            VisualPresentations[visualComponent] = uiPresentation;
         }
      }

      public void PushTexture(IUIComponent visualComponent, BitmapSource bitmap)
      {
         // if (VisualPresentations.TryGetValue(visualComponent, out var shapePresentation))
         // {
         //    if (shapePresentation[0].HasTexture)
         //    {
         //       var textured = shapePresentation[0];
         //       textured.Texture = bitmap.DXTexture;
         //    }
         // }
      }

      public void PushTexture(IUIComponent visualComponent, Texture bitmap)
      {
         // if (VisualPresentations.TryGetValue(visualComponent, out var shapePresentation))
         // {
         //    if (shapePresentation[0].HasTexture)
         //    {
         //       var textured = shapePresentation[0];
         //       textured.Texture = bitmap;
         //    }
         // }
      }

      public void DrawEmptyRectangle(IUIComponent visualComponent, Brush brush, Rect rect, Thickness borderThickness, Thickness cornerRadius)
      {
         
      }

      public void DrawFilledRectangle(IUIComponent visualComponent, Brush brush, Rect rect, Thickness cornerRadius)
      {

      }

   }
}
