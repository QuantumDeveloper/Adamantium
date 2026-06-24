using System;
using Adamantium.Graphics;
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
using Adamantium.Vulkan.Core;
using Buffer = Adamantium.Graphics.Buffer;

namespace Adamantium.UI.Rendering.RenderUnits;

public abstract class UIRenderComponent : DeferredDisposableObject
{
    // UI body geometry lives in mappable (BAR) memory and is reused across frames through the buffer manager: a
    // size/shape change rewrites the current frame's ring slot in place (UpdateGeometry) instead of allocating a fresh
    // Vulkan buffer. See GPU_BUFFER_REUSE_PLAN.
    private const MemoryPropertyFlags UiMemory = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal;
    private static readonly int VertexStride = System.Runtime.InteropServices.Marshal.SizeOf<UIVertex>();

    protected GpuBufferManager BufferManager { get; }
    private ReusableBuffer _vertexBuffer;
    private ReusableBuffer _indexBuffer;
    private UIVertex[] _vertices;
    private int[] _indices;
    private uint _vertexCount;
    private uint _indexCount;

    protected UIRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, Mesh mesh, GpuBufferManager bufferManager) : base(device)
    {
        GraphicsDevice = device;
        UIBasicEffect = uiBasicEffect;
        BufferManager = bufferManager;
        ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        VertexType = typeof(UIVertex);
        if (mesh != null) SetMesh(mesh);
    }

    public Mesh Mesh { get; private set; }

    public Type VertexType { get; set; }

    public PrimitiveType PrimitiveType { get; set; }

    public RenderData RenderData { get; set; }

    public UIBasicEffect UIBasicEffect { get; set; }

    protected IGraphicsDevice GraphicsDevice { get; private set; }

    public ColorBlendEquationEXT ColorBlendEquation { get; set; }

    // Re-point this component at new geometry of the same kind WITHOUT recreating it (or its buffers): the manager
    // rewrites the existing ring slot in place, growing only if the new geometry needs more room. This is the resize/
    // animation fast path - the unit calls it instead of building a fresh component (and a fresh allocation) per frame.
    public void UpdateGeometry(Mesh mesh) => SetMesh(mesh);

    private void SetMesh(Mesh mesh)
    {
        Mesh = mesh;
        if (mesh != null) PrimitiveType = mesh.MeshTopology;

        _vertices = mesh?.ToUIVertices();
        _vertexCount = _vertices is { Length: > 0 } ? (uint)_vertices.Length : 0u;
        if (_vertexCount > 0)
        {
            _vertexBuffer ??= ToDispose(BufferManager.CreateBuffer(BufferUsageFlags.VertexBuffer, UiMemory));
            _vertexBuffer.Reserve((ulong)(_vertexCount * (uint)VertexStride));   // size the allocation up front
            _vertexBuffer.Invalidate();   // new payload -> every slot rewrites lazily on its next frame (promotes if drawn)
        }

        _indices = mesh is { HasIndices: true } && _vertexCount > 0 ? mesh.Indices : null;
        _indexCount = _indices != null ? (uint)_indices.Length : 0u;
        if (_indexCount > 0)
        {
            _indexBuffer ??= ToDispose(BufferManager.CreateBuffer(BufferUsageFlags.IndexBuffer, UiMemory));
            _indexBuffer.Reserve((ulong)(_indexCount * sizeof(int)));
            _indexBuffer.Invalidate();
        }
    }

    public void Update(Matrix4x4F transform, Matrix4x4F projectionMatrix)
    {
        RenderData.TransformMatrix = transform;
        RenderData.ProjectionMatrix = projectionMatrix;
    }

    /// <summary>Out-of-render-pass work, recorded before BeginRendering. Base is a no-op.</summary>
    public virtual void PreRender() { }

    public virtual void Render()
    {
        if (_vertexCount == 0) return;

        // Rent this frame's ring slot; upload the geometry only if the slot is stale (a static body settles to zero
        // work after the first N frames, an animated one writes only the current slot - never a new allocation).
        var vertexBuffer = _vertexBuffer.Acquire((ulong)(_vertexCount * (uint)VertexStride), out var writeVertices);
        if (writeVertices) vertexBuffer.SetData(_vertices, 0, _vertexCount);

        GraphicsDevice.VertexType = VertexType;
        GraphicsDevice.PolygonMode = PolygonMode.Fill;
        GraphicsDevice.PrimitiveTopology = Mesh.MeshTopology;
        GraphicsDevice.ColorBlendEquation = ColorBlendEquation;
        GraphicsDevice.DepthCompareFunction = CompareOp.Always;
        GraphicsDevice.DepthTestEnabled = true;
        GraphicsDevice.DepthWriteEnable = true;

        if (_indexCount > 0)
        {
            var indexBuffer = _indexBuffer.Acquire((ulong)(_indexCount * sizeof(int)), out var writeIndices);
            if (writeIndices) indexBuffer.SetData(_indices, 0, _indexCount);
            // DrawIndexed binds both the vertex and index buffers itself - don't bind them again. The over-allocated
            // ring buffer may be larger than the geometry, so draw the actual index count, not the buffer's capacity.
            GraphicsDevice.DrawIndexed(vertexBuffer, indexBuffer, indexCount: _indexCount);
        }
        else
        {
            GraphicsDevice.SetVertexBuffer(vertexBuffer);
            GraphicsDevice.Draw(_vertexCount, 1);
        }
    }
}

public class StrokeRenderComponent : UIRenderComponent
{
    public StrokeRenderComponent(IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, Mesh mesh, Pen pen, GpuBufferManager bufferManager) : base(graphicsDevice, uiBasicEffect, mesh, bufferManager)
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
            var fill = solidColor.Color.ToVector4();
            fill.W *= (float)solidColor.Opacity;   // fold the brush's own Opacity into the colour alpha
            UIBasicEffect.FillColor.SetValue(fill);
            UIBasicEffect.BasicSolidColorPass.Apply();
        }
        base.Render();
    }
}

public class GeometryRenderComponent : UIRenderComponent
{
    public GeometryRenderComponent(IGraphicsDevice graphicsDevice, UIBasicEffect uiBasicEffect, Mesh mesh, Brush background, GpuBufferManager bufferManager) : base(graphicsDevice, uiBasicEffect, mesh, bufferManager)
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
            var fill = solidColor.Color.ToVector4();
            fill.W *= (float)solidColor.Opacity;   // fold the brush's own Opacity into the colour alpha
            UIBasicEffect.FillColor.SetValue(fill);
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
    public ImageRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, Mesh mesh, ITexture texture, GpuBufferManager bufferManager) : base(device, uiBasicEffect, mesh, bufferManager)
    {
        Texture = texture;
    }

    public ImageRenderComponent(IGraphicsDevice device, UIBasicEffect uiBasicEffect, Mesh mesh, Brush background, GpuBufferManager bufferManager) : base(device, uiBasicEffect, mesh, bufferManager)
    {
        Background = background;
    }
    
    public Brush Background { get; set; }

    public ITexture Texture { get; set; }

    public SamplerState Sampler { get; set; }

    /// <summary>When set, this image is backed by an externally produced shared surface. Sampled via the private
    /// <see cref="UIRenderComponent.Texture"/>, refreshed each frame by the latch in <see cref="PreRender"/>.</summary>
    public SharedSurface SharedSource { get; set; }
    private ulong _lastLatched;

    // The shared surface (Texture) is sampled directly. Register, for this frame's UI Submit: a wait on
    // Produce>=latest (so the producer's write+transition-to-ShaderReadOnly is complete before the fragment sample)
    // and a signal of Consume=latest (so the producer may reuse the surface). Producer/consumer run one frame in
    // lockstep (the producer CPU-throttles on Consume), so there is no read-during-write race.
    public override void PreRender()
    {
        if (SharedSource == null) return;
        var latest = SharedSource.ProduceValue;
        if (latest <= _lastLatched) return;
        GraphicsDevice.AddWaitSemaphore(SharedSource.ProduceSemaphore, PipelineStageFlagBits.FragmentShaderBit, latest);
        GraphicsDevice.AddSignalSemaphore(SharedSource.ConsumeSemaphore, latest);
        _lastLatched = latest;
    }

    public override void Render()
    {
        var world = RenderData.TransformMatrix;;
        UIBasicEffect.Wvp.SetValue(world * RenderData.ProjectionMatrix);
        UIBasicEffect.Opacity.SetValue(RenderData.Opacity);
        
        if (Background is SolidColorBrush solidColor)
        {
            var fill = solidColor.Color.ToVector4();
            fill.W *= (float)solidColor.Opacity;   // fold the brush's own Opacity into the colour alpha
            UIBasicEffect.FillColor.SetValue(fill);
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
    private const float TextSupersample = 1f;

    public TextRenderComponent(IGraphicsDevice device,
        UIBasicEffect uiBasicEffect,
        Mesh mesh,
        FontRenderer fontRenderer,
        TextLayout textLayout,
        TextRenderingParameters renderingParameters, 
        Brush background,
        Brush foreground,
        Brush stroke,
        GpuBufferManager bufferManager) : base(device, uiBasicEffect, mesh, background, bufferManager)
    {
        FontRenderer = fontRenderer;
        TextLayout = textLayout;
        RenderingParameters = renderingParameters;
        Foreground = foreground;
        Stroke = stroke;
        // Supersampled target: TextSupersample x the logical text size. Must scale together with
        // FontRenderer.RenderScale (set in Render) — RT and rasterization scale have to match or the glyphs
        // and the target disagree (the earlier "crumpled" SSAA was exactly this mismatch).
        _renderTarget = ToDispose(device.CreateRenderTarget((uint)(mesh.Bounds.Width * TextSupersample),
            (uint)(mesh.Bounds.Height * TextSupersample),
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

    // Colour-only change: swap the brushes and force one re-rasterization, reusing the existing render
    // target and geometry (no buffer/RT rebuild).
    public void UpdateColors(Brush background, Brush foreground, Brush stroke)
    {
        Background = background;
        Foreground = foreground;
        Stroke = stroke;
        _textRendered = false;
    }

    public override void Render()
    {
        // Inset the text by the effect padding inside the (padded) target so edge glyphs' outline/glow have
        // room. The composite quad was grown by the same pad with its origin shifted -pad (see RenderUnit),
        // which cancels this inset, keeping the text body in its exact on-screen position.
        var pad = TextLayout.EffectPadding;
        var location = new Vector3F(RenderingParameters.TextArea.X + pad, RenderingParameters.TextArea.Y + pad, 5);
        
        var resolveTexture = _renderTarget.ResolveTexture;
        if (!_textRendered)
        {
            var foreground = ((SolidColorBrush)Foreground).Color;
            var stroke = ((SolidColorBrush)Stroke).Color;
            var previousColor = GraphicsDevice.ClearColor;
            stroke = Colors.Transparent;
            //Background = new SolidColorBrush(Colors.Transparent);
            // Rasterize the (unchanged, logical-size) layout RenderScale x larger into the supersampled
            // target; the composite below minifies it back = SSAA. Must equal the RT's TextSupersample.
            FontRenderer.RenderScale = TextSupersample;
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