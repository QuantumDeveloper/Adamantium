using System;
using System.Collections.Generic;
using System.Linq;
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
   private uint _currentIndex;

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
      //currentContainer?.DisposeAndClearItems();
      _currentIndex = 0;
      currentComponent = visualComponent;
   }

   internal void EndDraw()
   {
      if (currentContainer.ChildUnits.Count > 0)
      {
         visualPresentations[currentComponent] = currentContainer;
      }

      if (_currentIndex < currentContainer.ChildUnits.Count)
      {
         int itemsToRemove = currentContainer.ChildUnits.Count - (int)_currentIndex;
         for (int i = itemsToRemove; i >= 0; --i)
         {
            currentContainer.ChildUnits[i].Dispose();
            currentContainer.ChildUnits.RemoveAt(i);
         }
      }

      currentContainer = null;
      currentUnit = null;
      currentComponent = null;
   }

   public void DrawRectangle(Brush brush, Rect destinationRect, Pen pen = null)
   {
      DrawRectangle(brush, destinationRect, CornerRadius.Empty, pen);
   }

   public void DrawRectangle(Brush brush, Rect destinationRect, CornerRadius corners, Pen pen = null)
   {
      if (currentContainer.ChildUnits.Count == 0)
      {
         var rectangle = new RectangleGeometry(destinationRect, corners);
         StrokeGeometry strokeGeometry = null;
         if (pen is { Thickness: > 0.0 })
         {
            strokeGeometry = new StrokeGeometry(pen, rectangle);
            strokeGeometry.ProcessGeometry(GeometryType.Solid);
         }

         rectangle.ProcessGeometry(GeometryType.Solid);
         currentUnit = new RenderUnit();
         currentUnit.GeometryRenderer =
            ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, rectangle.Mesh, brush);

         if (strokeGeometry != null)
         {
            var strokeRenderer =
               ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
            currentUnit.StrokeRenderer = strokeRenderer;
         }

         currentContainer.AddItem(currentUnit);
      }
      else
      {
         var unit = currentContainer.ChildUnits.FirstOrDefault(x =>
            x.GeometryMetadata.Rectangle == destinationRect && x.GeometryMetadata.Corners == corners);
         if (unit != null)
         {
            var index = currentContainer.ChildUnits.IndexOf(unit);
            unit.GeometryRenderer.Brush = brush;
            if (index != _currentIndex)
            {
               currentContainer.ChildUnits.Insert((int)_currentIndex, unit);
            }
         }
         else
         {
            var geometry = new RectangleGeometry(destinationRect, corners);
            geometry.ProcessGeometry(GeometryType.Solid);
            var uiRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, geometry.Mesh, brush);
            currentUnit = new RenderUnit();
            currentUnit.GeometryMetadata.Rectangle = destinationRect;
            currentUnit.GeometryMetadata.Corners = corners;
            currentUnit.GeometryRenderer = uiRenderer;
            
            currentContainer.ChildUnits[(int)_currentIndex].Dispose();
            currentContainer.ChildUnits[(int)_currentIndex] = currentUnit;
         }
      }

      _currentIndex++;
   }
      
   public void DrawEllipse(Rect destinationRect, Brush brush, Double startAngle, Double stopAngle, Pen pen = null)
   {
      if (currentContainer.ChildUnits.Count == 0)
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
         currentUnit.GeometryMetadata.Rectangle = destinationRect;
         currentUnit.GeometryMetadata.StartAngle = startAngle;
         currentUnit.GeometryMetadata.StopAngle = stopAngle;

         if (strokeGeometry != null && strokeGeometry.Mesh.HasPoints)
         {
            var strokeRenderer =
               ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
            currentUnit.StrokeRenderer = strokeRenderer;
         }

         currentContainer?.AddItem(currentUnit);
      }
      else
      {
         var unit = currentContainer.ChildUnits.FirstOrDefault(x =>
            x.GeometryMetadata.Rectangle == destinationRect && 
            MathHelper.NearEqual(x.GeometryMetadata.StartAngle, startAngle) &&
            MathHelper.NearEqual(x.GeometryMetadata.StopAngle, stopAngle));
         if (unit != null)
         {
            var index = currentContainer.ChildUnits.IndexOf(unit);
            unit.GeometryRenderer.Brush = brush;
            if (index != _currentIndex)
            {
               currentContainer.ChildUnits.Insert((int)_currentIndex, unit);
            }
         }
         else
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
            currentUnit.GeometryMetadata.Rectangle = destinationRect;
            currentUnit.GeometryMetadata.StartAngle = startAngle;
            currentUnit.GeometryMetadata.StopAngle = stopAngle;

            if (strokeGeometry != null && strokeGeometry.Mesh.HasPoints)
            {
               var strokeRenderer =
                  ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, strokeGeometry?.Mesh, pen?.Brush);
               currentUnit.StrokeRenderer = strokeRenderer;
            }
            
            currentContainer.ChildUnits[(int)_currentIndex].Dispose();
            currentContainer.ChildUnits[(int)_currentIndex] = currentUnit;
         }
      }

      _currentIndex++;
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
      _currentIndex++;
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
      _currentIndex++;
   }

   public void DrawImage(ImageSource image, Brush filter, Rect destinationRect, CornerRadius corners)
   {
      if (image is BitmapSource bitmapSource)
      {
         bitmapSource.InitUnderlyingImage(this);
      }

      if (currentContainer.ChildUnits.Count == 0)
      {
         var geometry = new RectangleGeometry(destinationRect, corners);
         geometry.ProcessGeometry(GeometryType.Solid);

         var uiRenderer = ComponentRenderFactory.CreateImageRenderer(GraphicsDevice, geometry.Mesh, filter, image);
         currentUnit = new RenderUnit();
         currentUnit.GeometryMetadata.Rectangle = destinationRect;
         currentUnit.GeometryMetadata.Corners = corners;
         currentUnit.GeometryRenderer = uiRenderer;
         currentContainer?.AddItem(currentUnit);
      }
      else
      {
         var unit = currentContainer.ChildUnits.FirstOrDefault(x =>
            x.GeometryMetadata.Rectangle == destinationRect && x.GeometryMetadata.Corners == corners);
         if (unit != null)
         {
            var index = currentContainer.ChildUnits.IndexOf(unit);
            ((ImageRenderer)unit.GeometryRenderer).Image = image;
            unit.GeometryRenderer.Brush = filter;
            if (index != _currentIndex)
            {
               currentContainer.ChildUnits.Insert((int)_currentIndex, unit);
            }
         }
         else
         {
            var geometry = new RectangleGeometry(destinationRect, corners);
            geometry.ProcessGeometry(GeometryType.Solid);
            var uiRenderer = ComponentRenderFactory.CreateImageRenderer(GraphicsDevice, geometry.Mesh, filter, image);
            currentUnit = new RenderUnit();
            currentUnit.GeometryMetadata.Rectangle = destinationRect;
            currentUnit.GeometryMetadata.Corners = corners;
            currentUnit.GeometryRenderer = uiRenderer;
            
            currentContainer.ChildUnits[(int)_currentIndex].Dispose();
            currentContainer.ChildUnits[(int)_currentIndex] = currentUnit;
         }
      }

      _currentIndex++;
   }

   public void AddImage(ImageSource imageSource)
   {
      currentUnit.Image = imageSource;
   }
}