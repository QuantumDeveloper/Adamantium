using System;
using System.Collections.Generic;
using Adamantium.Engine.Graphics;
using Adamantium.UI.Media.Imaging;
using Adamantium.UI.Rendering;
using Serilog;

namespace Adamantium.UI.Media;

public class DrawingContext
{
   public GraphicsDevice GraphicsDevice { get; }
      
   private readonly Dictionary<IUIComponent, UIRenderContainer> visualPresentations;

   private UIRenderContainer currentContainer;
   private RenderUnit currentUnit;
   private IUIComponent currentComponent;

   internal DrawingContext(GraphicsDevice d3dDevice)
   {
      GraphicsDevice = d3dDevice;
      visualPresentations = new Dictionary<IUIComponent, UIRenderContainer>();
   }

   internal bool GetContainerForComponent(IUIComponent component, out UIRenderContainer container)
   {
      return visualPresentations.TryGetValue(component, out container);
   }

   internal void BeginDraw(IUIComponent visualComponent)
   {
      if (!visualPresentations.TryGetValue(visualComponent, out currentContainer))
      {
         currentContainer = new UIRenderContainer();
      }
      currentContainer?.DisposeAndClearItems();
      
      currentComponent = visualComponent;
   }

   internal void EndDraw()
   {
      if (currentContainer.ChildUnits.Count > 0)
      {
         visualPresentations[currentComponent] = currentContainer;
      }

      currentContainer = null;
      currentUnit = null;
      currentComponent = null;
   }

   public void DrawRectangle(Brush brush, Rect rect, Pen pen = null)
   {
      DrawRectangle(brush, rect, CornerRadius.Empty, pen);
   }

   public void DrawRectangle(Brush brush, Rect rect, CornerRadius corners, Pen pen = null)
   {
      var rectangle = new RectangleGeometry(rect, corners);
      StrokeGeometry strokeGeometry = null;
      if (pen is { Thickness: > 0.0 })
      {
         strokeGeometry = new StrokeGeometry(pen, rectangle);
         strokeGeometry.ProcessGeometry(GeometryType.Solid);
      }

      rectangle.ProcessGeometry(GeometryType.Solid);
      currentUnit = new RenderUnit();
      currentUnit.GeometryRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, rectangle.Mesh, brush);
      

      if (strokeGeometry != null)
      {
         var strokeRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
         currentUnit.StrokeRenderer = strokeRenderer;
      }

      currentContainer.AddItem(currentUnit);
   }
      
   public void DrawEllipse(Rect destinationRect, Brush brush, Double startAngle, Double stopAngle, Pen pen = null)
   {
      var ellipse = new EllipseGeometry(destinationRect, startAngle, stopAngle);
      ellipse.ProcessGeometry(GeometryType.Both);
      StrokeGeometry strokeGeometry = null;
      if (pen != null && pen.Thickness > 0.0)
      {
         strokeGeometry = new StrokeGeometry(pen, ellipse);
         strokeGeometry.ProcessGeometry(GeometryType.Solid);
      }

      currentUnit = new RenderUnit();
         
      var uiRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, ellipse.Mesh, brush);
      currentUnit.GeometryRenderer = uiRenderer;

      if (strokeGeometry != null && strokeGeometry.Mesh.HasPoints)
      {
         var strokeRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
         currentUnit.StrokeRenderer = strokeRenderer;
      }
         
      currentContainer?.AddItem(currentUnit);
   }

   public void DrawGeometry(Brush brush, Geometry geometry, Pen pen = null)
   {
      if (geometry == null) return;
      
      geometry.ProcessGeometry(GeometryType.Both);
      StrokeGeometry strokeGeometry = null;
      if (pen is { Thickness: > 0.0 })
      {
         strokeGeometry = new StrokeGeometry(pen, geometry);
         strokeGeometry.ProcessGeometry(GeometryType.Solid);
      }

      currentUnit = new RenderUnit();
      if (geometry.Mesh.HasPoints)
      {
         var uiRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, geometry.Mesh, brush);
         currentUnit.GeometryRenderer = uiRenderer;
      }

      if (strokeGeometry != null && strokeGeometry.Mesh.HasPoints)
      {
         var strokeRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
         currentUnit.StrokeRenderer = strokeRenderer;
      }
      
      currentContainer?.AddItem(currentUnit);
   }

   public void DrawLine(Vector2 start, Vector2 end, Pen pen)
   {
      if (pen is { Thickness: > 0.0 }) return;
      
      var geometry = new LineGeometry(start, end);
      geometry.ProcessGeometry(GeometryType.Solid);
         
      var strokeGeometry = new StrokeGeometry(pen, geometry);
      strokeGeometry.ProcessGeometry(GeometryType.Solid);

      currentUnit = new RenderUnit();
         
      var strokeRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, strokeGeometry.Mesh, pen.Brush);
      currentUnit.StrokeRenderer = strokeRenderer;
         
      currentContainer?.AddItem(currentUnit);
   }

   public void DrawImage(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners)
   {
      if (image is BitmapSource bitmapSource)
      {
         bitmapSource.InitUnderlyingImage(this);
      }
      
      var geometry = new RectangleGeometry(destinationRect, corners);
      geometry.ProcessGeometry(GeometryType.Solid);

      var uiRenderer = ComponentRenderFactory.CreateImageRenderer(GraphicsDevice, geometry.Mesh, filter, image);
      currentUnit = new RenderUnit();
      currentUnit.GeometryRenderer = uiRenderer;
      currentContainer?.AddItem(currentUnit);
   }

   public void AddImage(ImageSource imageSource)
   {
      currentUnit.Image = imageSource;
   }
}