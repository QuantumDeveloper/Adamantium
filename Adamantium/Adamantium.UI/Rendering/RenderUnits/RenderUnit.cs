using System;
using System.Collections.Generic;
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
        StrokeEffect strokeEffect,
        IResourceFactory resourceFactory) : base(graphicsDevice)
    {
        DrawCommand = command;
        Payload = DrawCommand.Payload as TPayload;
        GraphicsDevice = graphicsDevice;
        ResourceFactory = resourceFactory;
        UIBasicEffect = uiBasicEffect;
        StrokeEffect = strokeEffect;
    }

    public TPayload Payload { get; protected set; }

    public UIBasicEffect UIBasicEffect { get; }

    // Global toggle: lets the stroke fall back to the CPU path on the fly if anything is wrong with the GPU path.
    public static bool UseGpuStroke { get; set; } = true;

    protected StrokeEffect StrokeEffect { get; }

    public UIRenderComponent StrokeRenderer { get; set; }

    public UIRenderComponent GeometryRenderer { get; set; }

    protected void ProcessStrokeData(Pen pen, Geometry geometry)
    {
        if (pen == null) return;

        StrokeRenderer?.DeferDispose();

        // The GPU path handles any solid-colour stroke: one OR many contours (combined/group geometry, shapes with
        // holes), open or closed, with dashes/trim and every cap/join - all as real geometry. Only a non-solid brush
        // (e.g. a gradient stroke) still falls back to the CPU StrokeGeometry path.
        if (UseGpuStroke && StrokeEffect != null && TryGetSolidContours(pen, geometry, out var contours))
        {
            StrokeRenderer = new GpuStrokeRenderComponent(GraphicsDevice, UIBasicEffect, StrokeEffect, contours, pen);
        }
        else
        {
            var strokeGeometry = new StrokeGeometry(pen, geometry);
            StrokeRenderer = new StrokeRenderComponent(GraphicsDevice, UIBasicEffect, strokeGeometry.Mesh, pen);
        }
        StrokeRenderer.RenderData = DrawCommand.RenderData;
    }

    // Every outline contour of a solid-colour stroke (combined/group geometry yields several), each as its raw points
    // plus whether it is a closed loop. False (CPU fallback) only for a non-solid brush or a geometry with no contour.
    private static bool TryGetSolidContours(Pen pen, Geometry geometry, out List<(Adamantium.Mathematics.Vector2[] Points, bool IsClosed)> contours)
    {
        contours = null;
        if (pen.Brush is not SolidColorBrush) return false;

        var mesh = geometry?.Mesh;
        if (mesh == null || mesh.Contours.Count == 0) return false;

        List<(Adamantium.Mathematics.Vector2[], bool)> list = [];
        foreach (var contour in mesh.Contours)
            if (contour.Points is { Length: >= 2 })
                list.Add((contour.Points, contour.IsGeometryClosed));

        if (list.Count == 0) return false;
        contours = list;
        return true;
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

    public virtual void PreRender()
    {
        GeometryRenderer?.PreRender();
        StrokeRenderer?.PreRender();
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

    protected override void Dispose(bool disposeManagedResources)
    {
        // The unit owns its renderers (their vertex/index buffers and text render targets), so dispose them too.
        // The unit itself is disposed via the deferred queue (after the frame fence), so disposing them now is safe.
        GeometryRenderer?.Dispose();
        StrokeRenderer?.Dispose();
        base.Dispose(disposeManagedResources);
    }
}

public class GeometryRenderUnit : RenderUnit<GeometryPayload>
{
    public GeometryRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, StrokeEffect strokeEffect, IResourceFactory resourceFactory) :
        base(command, graphicsDevice, uiBasicEffect, strokeEffect, resourceFactory)
    {
        Payload.Geometry.ProcessGeometry(GeometryType.Both);
        GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, Payload.Geometry.Mesh, Payload.Brush);
        GeometryRenderer.RenderData = DrawCommand.RenderData;
        ProcessStrokeData(Payload.Pen, Payload.Geometry);
    }

    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not GeometryPayload inputPayload) return;

        // Capture the old pen BEFORE reassigning Payload, otherwise the comparison below is always equal.
        var oldPen = Payload.Pen;
        var rebuild = Payload.RequiresBufferRebuild(inputPayload);

        if (rebuild)
        {
            inputPayload.Geometry.ProcessGeometry(GeometryType.Both);
            GeometryRenderer?.DeferDispose();
            GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, inputPayload.Geometry.Mesh, inputPayload.Brush);
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

        // The stroke wraps the geometry, so rebuild it when the geometry changed OR the pen changed.
        if (rebuild || !Equals(oldPen, inputPayload.Pen))
        {
            ProcessStrokeData(inputPayload.Pen, inputPayload.Geometry);
        }
    }
}

public class LineRenderUnit : RenderUnit<LinePayload>
{
    public LineRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, StrokeEffect strokeEffect, IResourceFactory resourceFactory) :
        base(command, graphicsDevice, uiBasicEffect, strokeEffect, resourceFactory)
    {
        var geometry = new LineGeometry(Payload.LineStart, Payload.LineEnd);
        geometry.ProcessGeometry(GeometryType.Both);
        ProcessStrokeData(Payload.Pen, geometry);
    }
    
    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not LinePayload inputPayload) return;

        // A line is pure stroke: its endpoints AND pen both feed RequiresBufferRebuild, so this single check
        // covers a move/resize as well as a pen change.
        var rebuild = Payload.RequiresBufferRebuild(inputPayload);

        DrawCommand = drawCommand;
        Payload = inputPayload;

        if (rebuild)
        {
            var geometry = new LineGeometry(inputPayload.LineStart, inputPayload.LineEnd);
            geometry.ProcessGeometry(GeometryType.Both);
            ProcessStrokeData(inputPayload.Pen, geometry);
        }
    }
}

public class RectangleRenderUnit : RenderUnit<RectanglePayload>
{
    public RectangleRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, StrokeEffect strokeEffect, IResourceFactory resourceFactory) :
        base(command, graphicsDevice, uiBasicEffect, strokeEffect, resourceFactory)
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

        var oldPen = Payload.Pen;
        var rebuild = Payload.RequiresBufferRebuild(inputPayload);

        var rectangleGeometry = new RectangleGeometry(inputPayload.DestinationRect, inputPayload.CornerRadius);
        if (rebuild)
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

        GeometryRenderer?.RenderData = DrawCommand.RenderData;

        // Stroke wraps the geometry: rebuild on geometry change OR pen change. In the pen-only case the
        // geometry wasn't processed above, so process it before building the stroke.
        if (rebuild || !Equals(oldPen, inputPayload.Pen))
        {
            if (!rebuild) rectangleGeometry.ProcessGeometry(GeometryType.Both);
            ProcessStrokeData(inputPayload.Pen, rectangleGeometry);
        }
    }
}

public class EllipseRenderUnit : RenderUnit<EllipsePayload>
{
    public EllipseRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, StrokeEffect strokeEffect, IResourceFactory resourceFactory) :
        base(command, graphicsDevice, uiBasicEffect, strokeEffect, resourceFactory)
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

        var oldPen = Payload.Pen;
        var rebuild = Payload.RequiresBufferRebuild(inputPayload);

        var ellipseGeometry = new EllipseGeometry(inputPayload.DestinationRect, inputPayload.StartAngle, inputPayload.SweepAngle,
            inputPayload.EllipseType);
        if (rebuild)
        {
            ellipseGeometry.ProcessGeometry(GeometryType.Both);
            GeometryRenderer?.DeferDispose();
            GeometryRenderer = new GeometryRenderComponent(GraphicsDevice, UIBasicEffect, ellipseGeometry.Mesh,
                inputPayload.Brush);
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
            GeometryRenderer.RenderData = drawCommand.RenderData;
        }

        // Stroke wraps the geometry: rebuild on geometry change OR pen change; process geometry first in
        // the pen-only case.
        if (rebuild || !Equals(oldPen, inputPayload.Pen))
        {
            if (!rebuild) ellipseGeometry.ProcessGeometry(GeometryType.Both);
            ProcessStrokeData(inputPayload.Pen, ellipseGeometry);
        }
    }
}

public class ImageRenderUnit : RenderUnit<ImagePayload>
{
    public ImageRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, StrokeEffect strokeEffect, IResourceFactory resourceFactory) :
        base(command, graphicsDevice, uiBasicEffect, strokeEffect, resourceFactory)
    {
        var rectangleGeometry = new RectangleGeometry(Payload.DestinationRect);
        // Generate the quad's vertices (every other render unit does this in its ctor too). Without it the mesh is
        // empty -> the component's VertexBuffer is null -> UIRenderComponent.Render early-returns and the image is
        // never drawn (this is why images never appeared while text/solid shapes did).
        rectangleGeometry.ProcessGeometry(GeometryType.Solid);
        if (Payload.Image is BitmapSource bitmapImage)
        {
            GeometryRenderer = CreateImageRenderer(rectangleGeometry.Mesh, bitmapImage);
            GeometryRenderer.RenderData = DrawCommand.RenderData;
        }
    }

    // BitmapSource (not just BitmapImage) so SharedSurfaceImage / RenderTargetImage also render. A live shared
    // surface (game→panel) is sampled directly and synchronised via its Produce/Consume timeline (see
    // ImageRenderComponent.SharedSource/PreRender); a regular bitmap is sampled directly.
    private ImageRenderComponent CreateImageRenderer(Adamantium.Graphics.Core.Models.Mesh mesh, BitmapSource image)
    {
        var texture = image.GetOrCreateTexture(ResourceFactory);
        var component = new ImageRenderComponent(GraphicsDevice, UIBasicEffect, mesh, texture)
        {
            Sampler = GraphicsDevice.SamplerStates.LinearClampToEdge
        };
        // A live shared surface (game→panel): sample it directly, and drive the producer/consumer timeline so the
        // sample never races the producer's write (see ImageRenderComponent.PreRender). Composited with the default
        // AlphaBlend so the panel's Opacity controls translucency; the producer now publishes an opaque frame
        // (alpha=1, see RenderingService.BeginDraw) so at Opacity=1 it is fully solid.
        if (texture is Adamantium.Graphics.SharedSurface shared)
        {
            component.SharedSource = shared;
        }
        return component;
    }

    public override void UpdateWithDrawCommand(IDrawCommand drawCommand)
    {
        if (drawCommand.Payload is not ImagePayload inputPayload) return;

        var rectangleGeometry = new RectangleGeometry(inputPayload.DestinationRect, inputPayload.CornerRadius);
        if (Payload.RequiresBufferRebuild(inputPayload))
        {
            rectangleGeometry.ProcessGeometry(GeometryType.Both);
            GeometryRenderer?.DeferDispose();
            if (inputPayload.Image is BitmapSource bitmapImage)
            {
                GeometryRenderer = CreateImageRenderer(rectangleGeometry.Mesh, bitmapImage);
            }
        }

        DrawCommand = drawCommand;
        Payload = inputPayload;

        GeometryRenderer?.RenderData = drawCommand.RenderData;
    }
}

public class TextRenderUnit : RenderUnit<TextPayload>
{
    public TextRenderUnit(IDrawCommand command, IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, StrokeEffect strokeEffect, IResourceFactory resourceFactory) :
        base(command, graphicsDevice, uiBasicEffect, strokeEffect, resourceFactory)
    {
        Payload.TextLayout.Update(GraphicsDevice);
        // Pad the text quad/RT so glyph effects (outline/glow) that reach beyond the body aren't clipped at
        // the block edges. The body stays put: the rect is grown symmetrically (origin shifted by -pad), so
        // its centre - and thus mesh-local (0,0) where the text is anchored - is unchanged.
        var pad = Payload.TextLayout.EffectPadding;
        var ds = Payload.DesiredSize;
        var rectangleGeometry = new RectangleGeometry(new Rect(-pad, -pad, ds.Width + 2 * pad, ds.Height + 2 * pad));
        rectangleGeometry.ProcessGeometry(GeometryType.Solid);
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
            inputPayload.TextLayout.Update(GraphicsDevice);
            var pad = inputPayload.TextLayout.EffectPadding;
            var ds = inputPayload.DesiredSize;
            var rectangleGeometry = new RectangleGeometry(new Rect(-pad, -pad, ds.Width + 2 * pad, ds.Height + 2 * pad));
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
        else if (!Equals(Payload.Background, inputPayload.Background) ||
                 !Equals(Payload.Foreground, inputPayload.Foreground) ||
                 !Equals(Payload.Stroke, inputPayload.Stroke))
        {
            // Geometry/layout unchanged - only the colours differ. Swap brushes and force a re-raster,
            // reusing the existing render target (RequiresBufferRebuild ignores colours, so without this
            // a colour-only change would never repaint). Compared against the old Payload, before reassign.
            ((TextRenderComponent)GeometryRenderer).UpdateColors(
                inputPayload.Background, inputPayload.Foreground, inputPayload.Stroke);
        }

        DrawCommand = drawCommand;
        Payload = inputPayload;
        GeometryRenderer.RenderData = drawCommand.RenderData;
    }
}