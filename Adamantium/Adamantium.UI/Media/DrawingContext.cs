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
         //int itemsToRemove = currentContainer.ChildUnits.Count - (int)_currentIndex;
         for (int i = currentContainer.ChildUnits.Count - 1; i >= (int)_currentIndex; --i)
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

   private void ProcessStrokeRenderer(Pen pen)
   {
      if (pen is { Thickness: > 0.0 } && currentUnit.StrokeParametersHash != HashCode.Combine(pen))
      {
         var strokeGeometry = new StrokeGeometry(pen, currentUnit.GeometryRenderer.Geometry);
         strokeGeometry.ProcessGeometry(GeometryType.Solid);
         var strokeRenderer =
            ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, strokeGeometry, pen?.Brush);
         currentUnit.StrokeParametersHash = HashCode.Combine(pen);
         currentUnit.StrokeRenderer = strokeRenderer;
      }
   }

   private void ProcessRectangleGeometry(Rect destinationRect, CornerRadius corners, Brush brush, int hash)
   {
      var rectangle = new RectangleGeometry(destinationRect, corners);
         
      rectangle.ProcessGeometry(GeometryType.Solid);
      currentUnit = new RenderUnit();
      currentUnit.GeometryParametersHash = hash;
      currentUnit.GeometryRenderer =
         ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, rectangle, brush);
   }
   
   private void ProcessImageGeometry(Rect destinationRect, CornerRadius corners, Brush brush, ImageSource image, int hash)
   {
      var rectangle = new RectangleGeometry(destinationRect, corners);
      rectangle.ProcessGeometry(GeometryType.Solid);
      
      currentUnit = new RenderUnit();
      currentUnit.GeometryParametersHash = hash;
      currentUnit.GeometryRenderer =
         ComponentRenderFactory.CreateImageRenderer(GraphicsDevice, rectangle, brush, image);
   }

   public void DrawRectangle(Brush brush, Rect destinationRect, CornerRadius corners, Pen pen = null)
   {
      var hash = HashCode.Combine(destinationRect, corners);
      if (currentContainer.ChildUnits.Count == 0)
      {
         currentUnit = new RenderUnit();
         ProcessRectangleGeometry(destinationRect, corners, brush, hash);
         ProcessStrokeRenderer(pen);

         currentContainer.AddItem(currentUnit);
      }
      else
      {
         currentUnit = currentContainer.ChildUnits.FirstOrDefault(x => x.GeometryParametersHash == hash);
         if (currentUnit != null)
         {
            var index = currentContainer.ChildUnits.IndexOf(currentUnit);
            currentUnit.GeometryRenderer.Brush = brush;
            if (index != _currentIndex)
            {
               currentContainer.ChildUnits.Insert((int)_currentIndex, currentUnit);
            }

            ProcessStrokeRenderer(pen);
         }
         else
         {
            ProcessRectangleGeometry(destinationRect, corners, brush, hash);
            ProcessStrokeRenderer(pen);
            
            currentContainer.ChildUnits[(int)_currentIndex].Dispose();
            currentContainer.ChildUnits[(int)_currentIndex] = currentUnit;
         }
      }

      _currentIndex++;
   }
      
   public void DrawEllipse(Rect destinationRect, Brush brush, Double startAngle, Double stopAngle, EllipseType ellipseType, Pen pen = null)
   {
      var hash = HashCode.Combine(destinationRect, startAngle, stopAngle);
      if (currentContainer.ChildUnits.Count == 0)
      {
         var ellipse = new EllipseGeometry(destinationRect, startAngle, stopAngle, ellipseType);
         ellipse.ProcessGeometry(GeometryType.Solid);
         
         currentUnit = new RenderUnit();
         var uiRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, ellipse, brush);
         currentUnit.GeometryRenderer = uiRenderer;
         currentUnit.GeometryParametersHash = hash;
         
         ProcessStrokeRenderer(pen);
         currentContainer?.AddItem(currentUnit);
      }
      else
      {
         var unit = currentContainer.ChildUnits.FirstOrDefault(x => x.GeometryParametersHash == hash);
         if (unit != null)
         {
            var index = currentContainer.ChildUnits.IndexOf(unit);
            unit.GeometryRenderer.Brush = brush;
            ProcessStrokeRenderer(pen);
            if (index != _currentIndex)
            {
               currentContainer.ChildUnits.Insert((int)_currentIndex, unit);
            }
         }
         else
         {
            var ellipse = new EllipseGeometry(destinationRect, startAngle, stopAngle, ellipseType);
            ellipse.ProcessGeometry(GeometryType.Both);

            currentUnit = new RenderUnit();
            var uiRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, ellipse, brush);
            currentUnit.GeometryRenderer = uiRenderer;
            currentUnit.GeometryParametersHash = hash;
            
            ProcessStrokeRenderer(pen);
            
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
         var uiRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, geometry, brush);
         currentUnit.GeometryRenderer = uiRenderer;
      }

      if (strokeGeometry != null && strokeGeometry.Mesh.HasPoints)
      {
         var strokeRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, strokeGeometry, pen?.Brush);
         currentUnit.StrokeRenderer = strokeRenderer;
      }

      if (currentUnit.GeometryRenderer != null || currentUnit.StrokeRenderer != null)
      {
         currentContainer?.AddItem(currentUnit);
         _currentIndex++;
      }
   }

   public void DrawLine(Vector2 start, Vector2 end, Pen pen)
   {
      if (pen is { Thickness: > 0.0 }) return;
      
      var geometry = new LineGeometry(start, end);
      geometry.ProcessGeometry(GeometryType.Solid);
         
      var strokeGeometry = new StrokeGeometry(pen, geometry);
      strokeGeometry.ProcessGeometry(GeometryType.Solid);

      currentUnit = new RenderUnit();
         
      var strokeRenderer = ComponentRenderFactory.CreateGeometryRenderer(GraphicsDevice, strokeGeometry, pen.Brush);
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
      var hash = HashCode.Combine(destinationRect, corners);

      if (currentContainer.ChildUnits.Count == 0)
      {
         ProcessImageGeometry(destinationRect, corners, filter, image, hash);
         currentContainer?.AddItem(currentUnit);
      }
      else
      {
         var unit = currentContainer.ChildUnits.FirstOrDefault(x =>
            x.GeometryParametersHash == hash);
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
            ProcessImageGeometry(destinationRect, corners, filter, image, hash);
            
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