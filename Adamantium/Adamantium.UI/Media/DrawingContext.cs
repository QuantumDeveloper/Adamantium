using System;
using System.Collections.Generic;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Adamantium.UI.Media.Imaging;
using Adamantium.UI.Rendering;

namespace Adamantium.UI.Media
{
   public class DrawingContext
   {
      internal GraphicsDevice GraphicsDevice { get; }
      
      private readonly Dictionary<IUIComponent, UIPresentationContainer> visualPresentations;

      private UIPresentationContainer currentContainer;
      private IUIComponent currentComponent;

      internal DrawingContext(GraphicsDevice d3dDevice)
      {
         GraphicsDevice = d3dDevice;
         visualPresentations = new Dictionary<IUIComponent, UIPresentationContainer>();
      }

      internal bool GetPresentationForComponent(IUIComponent component, out UIPresentationContainer container)
      {
         return visualPresentations.TryGetValue(component, out container);
      }

      public void BeginDraw(IUIComponent visualComponent)
      {
         if (visualPresentations.ContainsKey(visualComponent))
         {
            var presentation = visualPresentations[visualComponent];
            presentation.DisposeAndClearItems();
         }
         currentContainer = new UIPresentationContainer();
         currentComponent = visualComponent;
      }

      public void EndDraw(IUIComponent visualComponent)
      {
         visualPresentations[visualComponent] = currentContainer;
         currentContainer = null;
         currentComponent = null;
      }

      public void DrawRectangle(Brush brush, Rect rect, Pen pen = null)
      {
         var rectangle = new RectangleGeometry(rect, new CornerRadius(0));
         StrokeGeometry stroke = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            stroke = new StrokeGeometry(pen, rectangle);
         }

         var presentationItem = new UIPresentationItem();
         
         var uiRenderer = UIComponentRenderer.Create(GraphicsDevice, rectangle.Mesh, brush);
         presentationItem.GeometryRenderer = uiRenderer;

         if (stroke != null)
         {
            var strokeRenderer = UIComponentRenderer.Create(GraphicsDevice, stroke?.Mesh, pen?.Brush);
            presentationItem.StrokeRenderer = strokeRenderer;
         }
         
         currentContainer?.AddItem(presentationItem);
      }
      
      public void DrawEllipse(Rect destinationRect, Brush brush, Double startAngle, Double stopAngle, Pen pen = null)
      {
         var ellipse = new EllipseGeometry(destinationRect, startAngle, stopAngle);
         ellipse.ProcessGeometry();
         StrokeGeometry stroke = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            stroke = new StrokeGeometry(pen, ellipse);
         }

         var presentationItem = new UIPresentationItem();
         
         var uiRenderer = UIComponentRenderer.Create(GraphicsDevice, ellipse.Mesh, brush);
         presentationItem.GeometryRenderer = uiRenderer;

         if (stroke != null && stroke.Mesh.HasPoints)
         {
            var strokeRenderer = UIComponentRenderer.Create(GraphicsDevice, stroke?.Mesh, pen?.Brush);
            presentationItem.StrokeRenderer = strokeRenderer;
         }
         
         currentContainer?.AddItem(presentationItem);
      }

      public void DrawRectangle(Brush brush, Rect rect, CornerRadius corners, Pen pen = null)
      {
         var rectangle = new RectangleGeometry(rect, corners);
         rectangle.ProcessGeometry();
         StrokeGeometry strokeGeometry = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            strokeGeometry = new StrokeGeometry(pen, rectangle);
         }

         var presentationItem = new UIPresentationItem();
         var uiRenderer = UIComponentRenderer.Create(GraphicsDevice, rectangle.Mesh, brush);
         presentationItem.GeometryRenderer = uiRenderer;

         if (strokeGeometry != null && strokeGeometry.Mesh.HasPoints)
         {
            var strokeRenderer = UIComponentRenderer.Create(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
            presentationItem.StrokeRenderer = strokeRenderer;
         }
         
         currentContainer?.AddItem(presentationItem);
      }

      public void DrawGeometry(Brush brush, Geometry geometry, Pen pen = null)
      {
         if (geometry == null) return;
         
         geometry.ProcessGeometry();
         StrokeGeometry strokeGeometry = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            strokeGeometry = new StrokeGeometry(pen, geometry);
         }

         var presentationItem = new UIPresentationItem();
         var uiRenderer = UIComponentRenderer.Create(GraphicsDevice, geometry.Mesh, brush);
         presentationItem.GeometryRenderer = uiRenderer;
         
         if (strokeGeometry != null && strokeGeometry.Mesh.Points.Length > 0)
         {
            var strokeRenderer = UIComponentRenderer.Create(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
            presentationItem.StrokeRenderer = strokeRenderer;
         }
         
         currentContainer?.AddItem(presentationItem);
      }

      public void DrawLine(Vector2 start, Vector2 end, Pen pen)
      {
         var geometry = new LineGeometry(start, end);
         geometry.ProcessGeometry();
         
         StrokeGeometry strokeGeometry = null;
         if (pen != null && pen.Thickness > 0.0)
         {
            strokeGeometry = new StrokeGeometry(pen, geometry);
         }

         var presentationItem = new UIPresentationItem();
         
         if (strokeGeometry != null)
         {
            var strokeRenderer = UIComponentRenderer.Create(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
            presentationItem.StrokeRenderer = strokeRenderer;
         }
         
         // var uiRenderer = UIComponentRenderer.Create(GraphicsDevice, geometry.Mesh, brush);
         // presentationItem.GeometryRenderer = uiRenderer;
         currentContainer?.AddItem(presentationItem);
      }

      public void DrawPolyline(IEnumerable<Vector2> points, Pen pen)
      {
         var presentationItem = new UIPresentationItem();
         // StrokeGeometry strokeGeometry = new StrokeGeometry(pen, points, false);
         // var strokeRenderer = UIComponentRenderer.Create(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
         // presentationItem.StrokeRenderer = strokeRenderer;
         // currentContainer?.AddItem(presentationItem);
      }

      public void DrawImage(BitmapSource bitmap, Brush filter, Rect destinationRect, CornerRadius corners)
      {
         var geometry = new RectangleGeometry(destinationRect, corners);
         
         var uiRenderer = UIComponentRenderer.Create(GraphicsDevice, geometry.Mesh, filter);
         uiRenderer.Bitmap = bitmap;
         var presentationItem = new UIPresentationItem();
         presentationItem.GeometryRenderer = uiRenderer;
         currentContainer?.AddItem(presentationItem);
      }

      public void PushTexture(BitmapSource bitmap)
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

      public void PushTexture(Texture bitmap)
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

   }
}
