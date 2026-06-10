using System;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Graphics.Core.Models;
using Adamantium.Graphics.Core.Vertices;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Effects.Generated;
using AdamantiumVulkan.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.UI.Rendering.RenderUnits;

public abstract class UIRenderComponent : DeferredDisposableObject
{
    protected UIRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, Mesh mesh) : base(device)
    {
        GraphicsDevice = device;
        Mesh = mesh;
        UIBasicEffect = uiBasicEffect;
        ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        
        var vertices = mesh?.ToUIVertices();
        if (vertices != null && vertices.Length != 0)
        {
            VertexBuffer = ToDispose(Buffer.Vertex.New(device, vertices));
        }

        if (mesh is { HasIndices: true } && VertexBuffer != null)
        {
            IndexBuffer = ToDispose(Buffer.Index.New(device, mesh.Indices));
        }

        VertexType = typeof(UIVertex);
        if (mesh != null) PrimitiveType = mesh.MeshTopology;
    }
    
    public Mesh Mesh { get; }
    
    public Buffer VertexBuffer { get; private set; }
    
    public Buffer IndexBuffer { get; private set; }
    
    public Type VertexType { get; set; }
    
    public PrimitiveType PrimitiveType { get; set; }

    public bool HasIndexBuffer => IndexBuffer is { ElementCount: > 0 };
    
    public RenderData RenderData { get; set; }
    
    public UIBasicEffect UIBasicEffect { get; set; }
    
    protected IGraphicsDevice GraphicsDevice { get; private set; }
    
    public ColorBlendEquationEXT ColorBlendEquation { get; set; }


    public void Update(Matrix4x4F transform, Matrix4x4F projectionMatrix)
    {
        RenderData.TransformMatrix = transform;
        RenderData.ProjectionMatrix = projectionMatrix;
    }

    public virtual void Render()
    {
        if (VertexBuffer == null) return;
        
        GraphicsDevice.SetVertexBuffer(VertexBuffer);
        GraphicsDevice.VertexType = VertexType;
        GraphicsDevice.PolygonMode = PolygonMode.Fill;
        GraphicsDevice.PrimitiveTopology = Mesh.MeshTopology;
        GraphicsDevice.ColorBlendEquation = ColorBlendEquation;
        GraphicsDevice.DepthCompareFunction = CompareOp.Always;
        GraphicsDevice.DepthTestEnabled = true;
        GraphicsDevice.DepthWriteEnable = true;

        if (HasIndexBuffer)
        {
            GraphicsDevice.SetIndexBuffer(IndexBuffer);
            GraphicsDevice.DrawIndexed(VertexBuffer, IndexBuffer);
        }
        else
        {
            GraphicsDevice.Draw(VertexBuffer.ElementCount, 1);
        }
    }
}

public class StrokeRenderComponent : UIRenderComponent
{
    public StrokeRenderComponent(IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, Mesh mesh, Pen pen) : base(graphicsDevice, uiBasicEffect, mesh)
    {
        PrimitiveType = PrimitiveType.TriangleList;
        Pen = pen;
    }
    
    public Pen Pen {get; set; }

    public override void Render()
    {
        //var world = Matrix4x4F.Translation((float)RenderData.Location.X, (float)RenderData.Location.Y, 5);
        var world = RenderData.TransformMatrix;
        UIBasicEffect.Wvp.SetValue(world * RenderData.ProjectionMatrix);
        //UIBasicEffect.World.SetValue(world);
        UIBasicEffect.Opacity.SetValue(RenderData.Opacity);
        if (Pen.Brush is SolidColorBrush solidColor)
        {
            UIBasicEffect.FillColor.SetValue(solidColor.Color.ToVector4());
            UIBasicEffect.BasicSolidColorPass.Apply();
        }
        base.Render();
    }
}

public class GeometryRenderComponent : UIRenderComponent
{
    public GeometryRenderComponent(IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, Mesh mesh, Brush background) : base(graphicsDevice, uiBasicEffect, mesh)
    {
        Background = background;
    }
    
    public Brush Background { get; set; }
    
    public override void Render()
    {
        var world = RenderData.TransformMatrix;
        UIBasicEffect.Wvp.SetValue(world * RenderData.ProjectionMatrix);
        UIBasicEffect.Opacity.SetValue(RenderData.Opacity);
        if (Background is SolidColorBrush solidColor)
        {
            UIBasicEffect.FillColor.SetValue(solidColor.Color.ToVector4());
            if (solidColor == Brushes.Transparent)
            {
                UIBasicEffect.Opacity.SetValue(0f);
            }
            UIBasicEffect.BasicSolidColorPass.Apply();
        }
        
        base.Render();
    }
}

public class ImageRenderComponent : UIRenderComponent
{
    public ImageRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, Mesh mesh, ITexture texture) : base(device, uiBasicEffect, mesh)
    {
        Texture = texture;
    }
    
    public ImageRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, Mesh mesh, Brush background) : base(device, uiBasicEffect, mesh)
    {
        Background = background;
    }
    
    public Brush Background { get; set; }
    
    public ITexture Texture { get; set; }
    
    public SamplerState Sampler { get; set; }

    public override void Render()
    {
        var world = RenderData.TransformMatrix;;
        UIBasicEffect.Wvp.SetValue(world * RenderData.ProjectionMatrix);
        UIBasicEffect.Opacity.SetValue(RenderData.Opacity);
        
        if (Background is SolidColorBrush solidColor)
        {
            UIBasicEffect.FillColor.SetValue(solidColor.Color.ToVector4());
        }
        
        if (Texture == null)
        {
            if (Background is SolidColorBrush)
            {
                UIBasicEffect.BasicSolidColorPass.Apply();
            }
        }
        else
        {
            UIBasicEffect.ShaderTexture.SetResource(Texture);
            UIBasicEffect.SampleType.SetResource(Sampler);
            UIBasicEffect.BasicTexturedPass.Apply();
        }
        
        base.Render();
    }
}

public class TextRenderComponent : ImageRenderComponent
{
    private IRenderTarget _renderTarget;

    // Render text into a supersampled target (this factor larger), then let it minify when composited onto
    // the control = SSAA. The real fix for small unhinted text: gives sub-pixel stems enough pixels.
    private const float TextSupersample = 2f;

    public TextRenderComponent(IGraphicsDevice device,
        UIBasicEffect uiBasicEffect,
        Mesh mesh,
        FontRenderer fontRenderer,
        TextLayout textLayout,
        TextRenderingParameters renderingParameters, 
        Brush background, 
        Brush foreground,
        Brush stroke) : base(device, uiBasicEffect, mesh, background)
    {
        FontRenderer = fontRenderer;
        TextLayout = textLayout;
        RenderingParameters = renderingParameters;
        Foreground = foreground;
        Stroke = stroke;
        _renderTarget = ToDispose(device.CreateRenderTarget((uint)mesh.Bounds.Width,
            (uint)mesh.Bounds.Height,
            MSAALevel.X4,
            SurfaceFormat.R8G8B8A8.UNorm,
            name: "TextRenderer"));
        Sampler = GraphicsDevice.SamplerStates.LinearFont;
    }
    
    public FontRenderer FontRenderer { get; }
    public TextLayout TextLayout { get; }
    public TextRenderingParameters RenderingParameters { get; }
    public Brush Foreground { get; set; }
    
    public Brush Stroke { get; set; }
    
    private bool _textRendered = false;

    public override void Render()
    {
        var location = new Vector3F(RenderingParameters.TextArea.X, RenderingParameters.TextArea.Y, 5);
        
        var resolveTexture = _renderTarget.ResolveTexture;
        if (!_textRendered)
        {
            var foreground = ((SolidColorBrush)Foreground).Color;
            var stroke = ((SolidColorBrush)Stroke).Color;
            var previousColor = GraphicsDevice.ClearColor;
            stroke = Colors.Transparent;
            //Background = new SolidColorBrush(Colors.Transparent);
            FontRenderer.SetState(GraphicsDevice.SamplerStates.LinearFont, location, _renderTarget);
            FontRenderer.DrawLayout(TextLayout, foreground, stroke);
            FontRenderer.RestoreState();
            GraphicsDevice.ClearColor = previousColor;
            _textRendered = true;
        }

        Texture = resolveTexture;
        Sampler = GraphicsDevice.SamplerStates.LinearClampToEdge;
        //Background = new SolidColorBrush(Colors.Red);
        // The text target holds premultiplied color (the font shaders output rgb*alpha and it was rendered
        // with a premultiplied blend), so it must be composited with a premultiplied blend too. A straight
        // AlphaBlend here would multiply by alpha again -> the dark rim around the text.
        ColorBlendEquation = ColorBlendEquations.Premultiplied;
        base.Render();
    }
}