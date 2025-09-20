using System;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.ProceduralGeometry;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Media.Imaging;
using Adamantium.UI.Effects.Generated;
using Adamantium.UI.Rendering.Payloads;

namespace Adamantium.UI.Rendering.RenderUnits;

public abstract class RenderUnit<TPayload> : DeferredDisposableObject, IRenderUnit where TPayload : class
{
    protected RenderUnit(
        IDrawCommand command, 
        IGraphicsDevice graphicsDevice, 
        UIBasicEffect uiBasicEffect,
        IResourceFactory resourceFactory) : base(graphicsDevice)
    {
        DrawCommand = command;
        Payload = DrawCommand.Payload as TPayload;
        GraphicsDevice = graphicsDevice;
        ResourceFactory = resourceFactory;
        UIBasicEffect = uiBasicEffect;
    }

    public TPayload Payload { get; protected set; }
    
    public UIBasicEffect UIBasicEffect { get; }
    
    public UIRenderComponent StrokeRenderer { get; set; }
    
    public UIRenderComponent GeometryRenderer { get; set; }
    
    protected void ProcessStrokeData(Pen pen, Geometry geometry)
    {
        if (pen == null) return;
        
        var strokeGeometry = new StrokeGeometry(pen, geometry);
        StrokeRenderer?.Dispose();
        StrokeRenderer = new StrokeRenderComponent(GraphicsDevice, UIBasicEffect, strokeGeometry.Mesh, pen);
        StrokeRenderer.RenderData = DrawCommand.RenderData;
    }
    
    protected IResourceFactory ResourceFactory { get; set; }
    protected IDrawCommand DrawCommand { get; set; }
    protected IGraphicsDevice GraphicsDevice { get; }

    public IUIComponent Component => DrawCommand.Component;

    public void Update(Matrix4x4F transform, Matrix4x4F projection)
    {
        GeometryRenderer?.Update(transform, projection);
        StrokeRenderer?.Update(transform, projection);
    }

    public virtual void Render()
    {
        GeometryRenderer?.Render();
        StrokeRenderer?.Render();
    }
    
    public abstract void UpdateWithDrawCommand(IDrawCommand drawCommand);
    public virtual bool Match(IDrawCommand drawCommand)
    {
        return DrawCommand.Payload.GetType() == drawCommand.Payload.GetType(); 
    }
}

public class GeometryRenderUnit : RenderUnit<GeometryPayload>
{
    public GeometryRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, IResourceFactory resourceFactory) : 
        base(command, graphicsDevice, uiBasicEffect, resourceFactory)
    {
        Payload.Geometry.ProcessGeometry(GeometryType.Both);
        GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, Payload.Geometry.Mesh, Payload.Brush);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
        ProcessStrokeData(Payload.Pen, Payload.Geometry);
    }

    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not GeometryPayload inputPayload) return;
        
        Console.WriteLine($"Location: {drawCommand.RenderData.TransformMatrix.TranslationVector}");
        if (Payload.RequiresBufferRebuild(inputPayload))
        {
            inputPayload.Geometry.ProcessGeometry(GeometryType.Both);
            GeometryRenderer?.DeferDispose();
            GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, inputPayload.Geometry.Mesh, Payload.Brush);
        }
        else
        {
            var geometryRenderer = (GeometryRenderComponent)GeometryRenderer;
            geometryRenderer.Background = inputPayload.Brush;
        }
            
        DrawCommand = drawCommand;
        Payload = inputPayload;
        if (GeometryRenderer != null)
        {
            GeometryRenderer.RenderData = DrawCommand.RenderData;
        }
            
        if (!Equals(Payload.Pen, inputPayload.Pen))
        {
            ProcessStrokeData(Payload.Pen, inputPayload.Geometry);
        }
    }
}

public class LineRenderUnit : RenderUnit<LinePayload>
{
    public LineRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, IResourceFactory resourceFactory) : 
        base(command, graphicsDevice, uiBasicEffect, resourceFactory)
    {
        var geometry = new LineGeometry(Payload.LineStart, Payload.LineEnd);
        geometry.ProcessGeometry(GeometryType.Both);
        ProcessStrokeData(Payload.Pen, geometry);
    }
    
    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not LinePayload inputPayload) return;
        
        DrawCommand = drawCommand;
        Payload = inputPayload;
            
        if (!Equals(Payload.Pen, inputPayload.Pen))
        {
            var geometry = new LineGeometry(Payload.LineStart, Payload.LineEnd);
            geometry.ProcessGeometry(GeometryType.Both);
            ProcessStrokeData(Payload.Pen, geometry);
        }
    }
}

public class RectangleRenderUnit : RenderUnit<RectanglePayload>
{
    public RectangleRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, IResourceFactory resourceFactory) : 
        base(command, graphicsDevice, uiBasicEffect, resourceFactory)
    {
        var rectangleGeometry = new RectangleGeometry(Payload.DestinationRect, Payload.CornerRadius);
        rectangleGeometry.ProcessGeometry(GeometryType.Both);
        GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, rectangleGeometry.Mesh, Payload.Brush);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
        ProcessStrokeData(Payload.Pen, rectangleGeometry);
    }
    
    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not RectanglePayload inputPayload) return;
        
        var rectangleGeometry = new RectangleGeometry(inputPayload.DestinationRect, inputPayload.CornerRadius);
        if (Payload.RequiresBufferRebuild(inputPayload))
        {
            rectangleGeometry.ProcessGeometry(GeometryType.Both);
            GeometryRenderer?.DeferDispose();
            GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, rectangleGeometry.Mesh, inputPayload.Brush);
        }
        else
        {
            var renderer = (GeometryRenderComponent)GeometryRenderer;
            renderer.Background = inputPayload.Brush;
        }
        DrawCommand = drawCommand;
        Payload = inputPayload;

        if (GeometryRenderer != null)
        {
            GeometryRenderer.RenderData = DrawCommand.RenderData;
        }

        if (!Equals(Payload.Pen, inputPayload.Pen))
        {
            ProcessStrokeData(Payload.Pen, rectangleGeometry);
        }
    }
}

public class EllipseRenderUnit : RenderUnit<EllipsePayload>
{
    public EllipseRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, IResourceFactory resourceFactory) : 
        base(command, graphicsDevice, uiBasicEffect, resourceFactory)
    {
        var ellipseGeometry = new EllipseGeometry(Payload.DestinationRect, Payload.StartAngle, Payload.SweepAngle, Payload.EllipseType);
        ellipseGeometry.ProcessGeometry(GeometryType.Both);
        GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, ellipseGeometry.Mesh, Payload.Brush);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
        ProcessStrokeData(Payload.Pen, ellipseGeometry);
    }

    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not EllipsePayload inputPayload) return;
        
        var rectangleGeometry = new EllipseGeometry(inputPayload.DestinationRect, inputPayload.StartAngle, inputPayload.SweepAngle,
            Payload.EllipseType);
        if (Payload.RequiresBufferRebuild(inputPayload))
        {
            rectangleGeometry.ProcessGeometry(GeometryType.Both);
            GeometryRenderer?.DeferDispose();
            GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, rectangleGeometry.Mesh,
                inputPayload.Brush);
        }

        DrawCommand = drawCommand;
        Payload = inputPayload;

        if (GeometryRenderer != null)
        {
            GeometryRenderer.RenderData = drawCommand.RenderData;
        }

        if (!Equals(Payload.Pen, inputPayload.Pen))
        {
            ProcessStrokeData(Payload.Pen, rectangleGeometry);
        }
    }
}

public class ImageRenderUnit : RenderUnit<ImagePayload>
{
    public ImageRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, IResourceFactory resourceFactory) : 
        base(command, graphicsDevice, uiBasicEffect, resourceFactory)
    {
        var rectangleGeometry = new RectangleGeometry(Payload.DestinationRect);
        if (Payload.Image is BitmapImage bitmapImage)
        {
            var image = bitmapImage.GetOrCreateTexture(ResourceFactory);
            GeometryRenderer = new ImageRenderComponent(GraphicsDevice, UIBasicEffect, rectangleGeometry.Mesh, image);
            GeometryRenderer.RenderData = DrawCommand.RenderData;
        }
    }
    
    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not ImagePayload inputPayload) return;
        
        var rectangleGeometry = new RectangleGeometry(inputPayload.DestinationRect, inputPayload.CornerRadius);
        if (Payload.RequiresBufferRebuild(inputPayload))
        {
            rectangleGeometry.ProcessGeometry(GeometryType.Both);
            GeometryRenderer?.DeferDispose();
            if (Payload.Image is BitmapImage bitmapImage)
            {
                var image = bitmapImage.GetOrCreateTexture(ResourceFactory);
                GeometryRenderer = new ImageRenderComponent(GraphicsDevice, UIBasicEffect, rectangleGeometry.Mesh, image);
            }
        }

        DrawCommand = drawCommand;
        Payload = inputPayload;
            
        if (GeometryRenderer != null)
        {
            GeometryRenderer.RenderData = drawCommand.RenderData;
        }
    }
}

public class TextRenderUnit : RenderUnit<TextPayload>
{
    public TextRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, IResourceFactory resourceFactory) : 
        base(command, graphicsDevice, uiBasicEffect, resourceFactory)
    {
        var rectangleGeometry = new RectangleGeometry(Payload.DesiredSize);
        rectangleGeometry.ProcessGeometry(GeometryType.Solid);
        Payload.TextLayout.Update(GraphicsDevice);
        GeometryRenderer = new TextRenderComponent(GraphicsDevice, 
            UIBasicEffect,
            rectangleGeometry.Mesh,
            ResourceFactory.GetFontRenderer(GraphicsDevice), 
            Payload.TextLayout,
            Payload.TextRenderingParameters, 
            Payload.Background,
            Payload.Foreground,
            Payload.Stroke);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
    }
    
    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not TextPayload inputPayload) return;
        
        if (Payload.RequiresBufferRebuild(inputPayload))
        {
            var rectangleGeometry = new RectangleGeometry(inputPayload.DesiredSize);
            inputPayload.TextLayout.Update(GraphicsDevice);
            rectangleGeometry.ProcessGeometry(GeometryType.Both);
            GeometryRenderer?.DeferDispose();
            GeometryRenderer = new TextRenderComponent(GraphicsDevice, 
                UIBasicEffect,
                rectangleGeometry.Mesh,
                ResourceFactory.GetFontRenderer(GraphicsDevice), 
                inputPayload.TextLayout,
                inputPayload.TextRenderingParameters, 
                inputPayload.Background, 
                inputPayload.Foreground,
                inputPayload.Stroke);
        }
            
        DrawCommand = drawCommand;
        Payload = inputPayload;
        GeometryRenderer.RenderData = drawCommand.RenderData;
    }
}