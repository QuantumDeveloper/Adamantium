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
        _indices = mesh is { HasIndices: true } && _vertexCount > 0 ? mesh.Indices : null;
        _indexCount = _indices != null ? (uint)_indices.Length : 0u;

        // Do NOT allocate the GPU vertex/index buffer here. A BATCHED unit (e.g. a solid item-background rect) is drawn
        // by the RectBatch's ONE shared instanced buffer and never calls its own Render(), so allocating a per-unit
        // buffer per Border is pure waste - and hundreds of them (a big virtualized tile grid) exhaust device memory,
        // which is the vkAllocateMemory=ErrorOutOfDeviceMemory. The buffer is allocated LAZILY on the first INDIVIDUAL
        // Render (Acquire -> EnsureCapacity); a re-mesh of an already-drawn unit just invalidates so the next Acquire
        // re-uploads the new geometry (the resize/animation fast path is preserved for units that DO draw themselves).
        _vertexBuffer?.Invalidate();
        _indexBuffer?.Invalidate();
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

        // First INDIVIDUAL draw: lazily create the per-unit buffer (batched units never get here, so never allocate).
        // Rent this frame's ring slot; upload the geometry only if the slot is stale (a static body settles to zero
        // work after the first N frames, an animated one writes only the current slot - never a new allocation).
        _vertexBuffer ??= ToDispose(BufferManager.CreateBuffer(BufferUsageFlags.VertexBuffer, UiMemory));
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
            _indexBuffer ??= ToDispose(BufferManager.CreateBuffer(BufferUsageFlags.IndexBuffer, UiMemory));
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
        var world = RenderData.TransformMatrix;
        UIBasicEffect.Wvp.SetValue(world * RenderData.ProjectionMatrix);
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
        // No MSAA: the glyphs are MSDF-textured quads, so their edge anti-aliasing comes from the font pixel shader
        // (screenPxRange median) - NOT from coverage sampling. MSAA would only AA the quad's axis-aligned borders, i.e.
        // nothing useful, while costing 4x sample memory + a resolve on every text (re)rasterization. Single-sample.
        _renderTarget = ToDispose(device.CreateRenderTarget((uint)(mesh.Bounds.Width * TextSupersample),
            (uint)(mesh.Bounds.Height * TextSupersample),
            MSAALevel.None,
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

    // Text-content change at the SAME size: the shared TextLayout was re-shaped in place and its glyph buffer
    // re-uploaded, so just reuse this render target + geometry and force one re-rasterization - no new component and no
    // Vulkan render-target allocation (the live-text / recycled-list-row fast path). Same body as UpdateColors; the name
    // marks the intent at the call site.
    public void UpdateText(Brush background, Brush foreground, Brush stroke)
    {
        Background = background;
        Foreground = foreground;
        Stroke = stroke;
        _textRendered = false;
    }

    // Rasterize the glyphs into the private text target BEFORE the main render pass begins (this runs in the
    // beforeRenderPass PreRender phase). So the main pass is never interrupted per text block - it only composites the
    // pre-rastered target. SetState/RestoreState use outerPassActive:false: there is no main pass to end/resume yet
    // (BeginDraw begins it right after PreRender), we just render this one target as a standalone pass.
    public override void PreRender()
    {
        // Direct main-pass draw: glyphs are drawn straight into the main pass in Render() - no private-RT
        // rasterization pre-pass at all. See FontRenderer.UseDirectTextDraw + docs/TEXT_GLYPH_BATCH_PLAN.md §9.
        if (FontRenderer.UseDirectTextDraw) 
            return;
        
        if (_textRendered)
            return;

        // Inset the text by the effect padding inside the (padded) target so edge glyphs' outline/glow have room. The
        // composite quad was grown by the same pad with its origin shifted -pad (see RenderUnit), cancelling this inset.
        var pad = TextLayout.EffectPadding;
        var location = new Vector3F(RenderingParameters.TextArea.X + pad, RenderingParameters.TextArea.Y + pad, 5);
        var foreground = ((SolidColorBrush)Foreground).Color;
        var stroke = Colors.Transparent;
        var previousColor = GraphicsDevice.ClearColor;
        // Rasterize the (logical-size) layout RenderScale x larger into the target; the composite minifies it = SSAA.
        FontRenderer.RenderScale = TextSupersample;
        FontRenderer.SetState(GraphicsDevice.SamplerStates.LinearFont, location, _renderTarget, outerPassActive: false);
        FontRenderer.DrawLayout(TextLayout, foreground, stroke);
        FontRenderer.RestoreState(outerPassActive: false);
        GraphicsDevice.ClearColor = previousColor;
        _textRendered = true;
    }

    public override void Render()
    {
        if (FontRenderer.UseDirectTextDraw)
        {
            RenderDirect();
            return;
        }

        // The glyphs were rasterized into _renderTarget in PreRender (before the main pass); here we only composite it.
        Texture = _renderTarget.ResolveTexture;
        Sampler = GraphicsDevice.SamplerStates.LinearClampToEdge;
        // The text target holds premultiplied color (the font shaders output rgb*alpha, rendered with a premultiplied
        // blend), so composite it with a premultiplied blend too - a straight AlphaBlend would darken the edges (rim).
        ColorBlendEquation = ColorBlendEquations.Premultiplied;
        base.Render();
    }

    // CPU pre-transform text batch, Stage 1 (docs/TEXT_GLYPH_BATCH_PLAN.md §9): draw the glyphs directly into the main
    // pass instead of compositing a pre-rastered target. Glyph local pos g -> control-local (g + TextArea) -> world
    // (RenderData.TransformMatrix) -> clip (RenderData.ProjectionMatrix). The RT path's +pad/-pad cancel (RT origin was
    // at -pad, rasterization added +pad), so TextArea alone is the offset. Row-vector convention (FontEffect.fx does
    // mul(pos, MatrixTransform), and EffectParameter transposes the matrix on upload). RenderScale = 1: no supersample
    // target - the MSDF pixel shader self-anti-aliases from screen-space derivatives.
    private void RenderDirect()
    {
        var textArea = RenderingParameters.TextArea;
        var mvp = Matrix4x4F.Translation(textArea.X, textArea.Y, 5)
                  * RenderData.TransformMatrix
                  * RenderData.ProjectionMatrix;
        var foreground = ((SolidColorBrush)Foreground).Color;
        FontRenderer.RenderScale = 1f;
        FontRenderer.DrawLayoutDirect(
            GraphicsDevice.SamplerStates.LinearFont,
            TextLayout,
            foreground,
            mvp,
            RenderData.Opacity);
    }
}