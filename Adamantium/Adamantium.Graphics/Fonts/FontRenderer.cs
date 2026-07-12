using System;
using Adamantium.FX.Effects.Generated;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics.Fonts;

public class FontRenderer : GraphicsResource
{
    private FontEffect fontEffect;
    private bool beginCalled;
    private Matrix4x4F transformMatrix;
    private Type vertexType = typeof(FontItem);

    private const float FontSizeThreshold = 14;
    // Glyph weight: a signed-distance contour bias (see FontEffect.fx). 0 = exact outline, > 0 = thicker
    // stems, < 0 = thinner. Normalized units (the field spans PixelRange texels), useful range ~[-0.15, 0.15].
    // Applied inside the screenPxRange term, so it scales with size and does not haze the background.
    private float FontWeight = 0.0f;

    // Supersampling factor for text. The text render target is RenderScale x bigger than the logical text
    // area, so the ortho is divided by it (the viewport stays full size) -> glyphs rasterize RenderScale x
    // larger and get downsampled when the texture is composited onto the control = SSAA. 1 = off.
    public float RenderScale { get; set; } = 1f;

    // The LIVE text path (docs/TEXT_GLYPH_BATCH_PLAN.md §9): text draws straight into the main pass via a single
    // CPU-built MVP (Translation(TextArea) x World x Projection - the only driver-safe form on this Turing: no
    // indirect/indexed matrix in a graphics shader). Off = the old private-RT rasterize + composite fallback (A/B).
    public static bool UseDirectTextDraw = true;

    // Stage 2 of the text batch (docs/TEXT_GLYPH_BATCH_PLAN.md §9): aggregate MANY visible text blocks into ONE
    // DrawBatch - the actual FPS win (collapse ~N per-block draws to ~1). When off, batchable text falls back to the
    // per-block DrawLayoutDirect (Stage 1). Requires UseDirectTextDraw too; the aggregator/fallback live in RenderCache.
    public static bool UseTextBatch = true;

    // Selects the glyph pixel shader: true = canonical MSDF (Chlumsky screenPxRange, RenderMsdf pass),
    // false = the gradient-derivative AA (Render pass). See FontEffect.fx.
    public bool UseCanonicalMsdf { get; set; } = true;

    // Outline test pass (RenderMsdfOutline). When on, draws OutlineColor as a ring OutlineWidth (normalized
    // field units) outside each glyph - a functional check that the distance field is valid beyond the
    // contour (only works because PxRange was widened; a thin field would have no data out there).
    public bool UseOutline { get; set; } = false;
    public Color OutlineColor { get; set; } = Colors.Black;
    public float OutlineWidth { get; set; } = 0.15f;

    // True-SDF blend band, in atlas texels per screen pixel. Below Lo the glyph is magnified -> MSDF median
    // (sharp corners); above Hi it is minified -> single-channel true SDF (crisp small text where the median
    // would soften); blended between. Defaults are the physical 1:1 .. 2:1 minification points. Set Lo >= Hi
    // to disable the blend (stay pure MSDF) without touching the shader.
    public float SdfBlendLo { get; set; } = 1.0f;
    public float SdfBlendHi { get; set; } = 2.0f;

    private EffectParameter effectSampler;
    private EffectParameter effectTexture;
    private EffectParameter effectMatrixTransform;
    private EffectParameter effectUVCornerCoords;
    private EffectParameter effectForegroundColor;
    private EffectParameter effectFontSize;
    private EffectParameter effectFontSizeThreshold;
    private EffectParameter effectFontWeight;
    private EffectParameter effectPixelRange;
    private EffectParameter effectAtlasSize;
    private EffectParameter effectOutlineColor;
    private EffectParameter effectOutlineWidth;
    private EffectParameter effectSdfBlendLo;
    private EffectParameter effectSdfBlendHi;
    private EffectParameter effectGlyphInstances;   // BDA address of the per-instance GlyphItem storage buffer (instanced batch)
    private EffectParameter effectTransforms;       // BDA address of the transform table the glyph VS indexes by slot

    private Vector2F currentScreenSize;
    private static readonly Vector2F[] UVCornerCoords = [Vector2F.Zero, Vector2F.UnitX, Vector2F.UnitY, Vector2F.One];
    private Matrix4x4F finalMatrix;
    private SamplerState assignedSamplerState;
    private IRenderTarget renderTarget;

    private Color _oldClearColor;
    private IRenderTarget _oldRenderTargets;
    private IDepthStencilBuffer _oldDepthStencilBuffer;
    private Rect2D[] _savedScissors;
    private Viewport[] _savedViewports;

    public FontRenderer(IGraphicsDevice device) : base(device)
    {
        fontEffect = new FontEffect(device);

        effectSampler = fontEffect.TextureSampler;
        effectTexture = fontEffect.Texture;
        effectMatrixTransform = fontEffect.MatrixTransform;
        effectUVCornerCoords = fontEffect.TextureCornerCoords;
        effectForegroundColor = fontEffect.ForegroundColor;
        effectFontSize = fontEffect.FontSize;
        effectFontSizeThreshold = fontEffect.FontSizeThreshold;
        effectFontWeight = fontEffect.FontWeight;
        effectPixelRange = fontEffect.PxRange;
        effectAtlasSize = fontEffect.MSDFAtlasSize;
        effectOutlineColor = fontEffect.OutlineColor;
        effectOutlineWidth = fontEffect.OutlineWidth;
        effectSdfBlendLo = fontEffect.SdfBlendLo;
        effectSdfBlendHi = fontEffect.SdfBlendHi;
        effectGlyphInstances = fontEffect.GlyphInstancesAddress;
        effectTransforms = fontEffect.TransformsAddress;
    }

    public void DrawLayout(Buffer<FontItem> glyphs, uint count, FontAtlas atlas, float fontSize, Color foreground, Color stroke)
    {
        DrawInternal(glyphs, count, atlas, fontSize, foreground, stroke);
    }

    public void SetState(
        SamplerState samplerState,
        Vector3F translation,
        IRenderTarget renderTarget,
        bool outerPassActive = true)
    {
        if (beginCalled)
        {
            throw new Exception("You need to call RestoreState() before you can call Begin() again");
        }

        assignedSamplerState = samplerState;
        this.renderTarget = renderTarget;
        currentScreenSize = new Vector2F(renderTarget.Width, renderTarget.Height);
        transformMatrix = Matrix4x4F.Translation(translation);

        // Snapshot the viewport + scissor active in the pass we're about to interrupt, so RestoreState can put back the
        // exact clip (e.g. a virtualized list item's narrowed scissor) instead of resetting to the full attachment.
        _savedScissors = ((GraphicsDevice)GraphicsDevice).CurrentScissors;
        _savedViewports = ((GraphicsDevice)GraphicsDevice).CurrentViewports;

        // Inline (called mid main pass): end it so we can render into the text target. Pre-pass (called from PreRender,
        // before the main pass begins): there's no outer pass to end - skip it.
        if (outerPassActive) GraphicsDevice.EndDraw();

        _oldClearColor = GraphicsDevice.ClearColor;
        _oldRenderTargets = GraphicsDevice.CurrentRenderTarget;
        _oldDepthStencilBuffer = GraphicsDevice.CurrentDepthStencilBuffer;

        GraphicsDevice.ClearColor = Colors.Transparent;
        GraphicsDevice.SetRenderTargets(renderTarget);
        GraphicsDevice.SetDepthBuffer(null);
        var device = (GraphicsDevice)GraphicsDevice;
        device.TransitionImagesForRendering(device.CurrentCommandBuffer, device.CurrentRenderTarget, device.CurrentRenderTarget.ResolveTexture);
        GraphicsDevice.BeginRendering(GraphicsDevice.CurrentCommandBuffer);

        beginCalled = true;
    }

    private void DrawInternal(Buffer<FontItem> glyphs, uint count, FontAtlas atlas, float fontSize, Color foreground, Color stroke)
    {
        if (count == 0) return;

        var vp = new Viewport();
        vp.Width = currentScreenSize.X;
        vp.Height = currentScreenSize.Y;
        vp.MinDepth = 0;
        vp.MaxDepth = 1;

        var scissor = new Rect2D();
        scissor.Offset = new Offset2D();
        scissor.Extent = new Extent2D();
        scissor.Extent.Width = (uint)currentScreenSize.X;
        scissor.Extent.Height = (uint)currentScreenSize.Y;

        // Divide the ortho extent by RenderScale while the viewport stays the full (supersampled) target:
        // the logical-size layout then maps across the whole target and rasterizes RenderScale x larger.
        // Without this division RenderScale did nothing - the text drew at 1x in a corner of the 2x target.
        var orthoProjection = Matrix4x4F.OrthoOffCenter(0, currentScreenSize.X / RenderScale, 0, currentScreenSize.Y / RenderScale, 0f, 100000f);
        finalMatrix = transformMatrix * orthoProjection;
        GraphicsDevice.SetViewports(vp);
        GraphicsDevice.SetScissors(scissor);
        GraphicsDevice.ColorBlendEnabled = true;
        // The font pixel shaders output premultiplied color (rgb * alpha), so the target must use a
        // premultiplied blend. The old straight-alpha "Fonts" blend multiplied by alpha a second time,
        // darkening the anti-aliased edges into a rim around every glyph.
        GraphicsDevice.ColorBlendEquation = ColorBlendEquations.Premultiplied;
        GraphicsDevice.PrimitiveRestartEnable = true;
        GraphicsDevice.MSAALevel = renderTarget.MSAALevel;
        GraphicsDevice.DepthTestEnabled = false;
        GraphicsDevice.DepthWriteEnable = false;

        effectSampler.SetResource(assignedSamplerState);
        effectTexture.SetResource(atlas.Atlas);
        effectMatrixTransform.SetValue(finalMatrix);
        effectUVCornerCoords.SetValue(UVCornerCoords);
        effectForegroundColor.SetValue(foreground.ToVector4());
        effectFontSize.SetValue(fontSize);
        effectFontSizeThreshold.SetValue(FontSizeThreshold);
        effectFontWeight.SetValue(FontWeight);
        effectPixelRange.SetValue(atlas.PixelRange);
        effectAtlasSize.SetValue(new Vector2F(atlas.Atlas.Width, atlas.Atlas.Height));
        effectOutlineColor.SetValue(OutlineColor.ToVector4());
        effectOutlineWidth.SetValue(OutlineWidth);
        effectSdfBlendLo.SetValue(SdfBlendLo);
        effectSdfBlendHi.SetValue(SdfBlendHi);
        GraphicsDevice.VertexType = vertexType;
        GraphicsDevice.SetVertexBuffer(glyphs);   // the component's own buffer, uploaded from the FROZEN glyph run (no live layout)
        // Instanced quad: 4-vertex triangle strip per glyph (corners from SV_VertexID), one instance per glyph.
        GraphicsDevice.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        if (stroke == Colors.Transparent)
        {
            IEffectPass pass;
            if (UseOutline)
                pass = fontEffect.FontBatchRenderMsdfOutlinePass;
            else
                pass = UseCanonicalMsdf ? fontEffect.FontBatchRenderMsdfPass : fontEffect.FontBatchRenderPass;
            pass.Apply();
        }
        else
        {
            fontEffect.StrokeColor.SetValue(stroke.ToVector4());
            fontEffect.FontBatchStrokedTextPass.Apply();
        }

        GraphicsDevice.Draw(4, count);   // 4 strip verts x glyph instances (composite path)
    }

    // Direct main-pass glyph draw (CPU pre-transform batch, Stage 1, docs/TEXT_GLYPH_BATCH_PLAN.md §9): renders the
    // layout's glyphs straight into the CURRENT (main) pass with a single caller-supplied MVP, instead of rasterizing
    // into a private RT and compositing a quad. No SetState/RestoreState, no render-target switch, no viewport/scissor
    // override (the main pass's clip stands), MSAA left as the main pass set it. Depth matches the other main-pass UI
    // units (CompareOp.Always, test+write on) - NOT the RT path's depth-off (that target had no depth buffer).
    // Behind FontRenderer.UseDirectTextDraw. The MVP is built on the CPU (Translation(TextArea) x World x Projection)
    // because an indirect/indexed matrix in a graphics shader AVs vkCreateShadersEXT on this Turing - see the plan.
    public void DrawLayoutDirect(SamplerState samplerState, Buffer<FontItem> glyphs, uint count, FontAtlas atlas, float fontSize, Color foreground, Matrix4x4F mvp, float opacity)
    {
        if (count == 0) return;

        GraphicsDevice.ColorBlendEnabled = true;
        // Font shaders output premultiplied color (rgb * alpha) -> premultiplied blend (same as the RT path + composite).
        GraphicsDevice.ColorBlendEquation = ColorBlendEquations.Premultiplied;
        GraphicsDevice.PrimitiveRestartEnable = true;
        GraphicsDevice.DepthTestEnabled = true;
        GraphicsDevice.DepthWriteEnable = true;
        GraphicsDevice.DepthCompareFunction = CompareOp.Always;

        var fg = foreground.ToVector4();
        fg.W *= opacity;   // fold the element's Opacity (fade animation, dimmed container) into the glyph alpha

        effectSampler.SetResource(samplerState);
        effectTexture.SetResource(atlas.Atlas);
        effectMatrixTransform.SetValue(mvp);
        effectUVCornerCoords.SetValue(UVCornerCoords);
        effectForegroundColor.SetValue(fg);
        effectFontSize.SetValue(fontSize);
        effectFontSizeThreshold.SetValue(FontSizeThreshold);
        effectFontWeight.SetValue(FontWeight);
        effectPixelRange.SetValue(atlas.PixelRange);
        effectAtlasSize.SetValue(new Vector2F(atlas.Atlas.Width, atlas.Atlas.Height));
        effectOutlineColor.SetValue(OutlineColor.ToVector4());
        effectOutlineWidth.SetValue(OutlineWidth);
        effectSdfBlendLo.SetValue(SdfBlendLo);
        effectSdfBlendHi.SetValue(SdfBlendHi);
        GraphicsDevice.VertexType = vertexType;
        GraphicsDevice.SetVertexBuffer(glyphs);   // the component's own buffer, uploaded from the FROZEN glyph run (no live layout)
        GraphicsDevice.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        // Direct text is never stroked (both text paths pass no stroke) - pick outline, else canonical / gradient MSDF.
        var pass = UseOutline
            ? fontEffect.FontBatchRenderMsdfOutlinePass
            : (UseCanonicalMsdf ? fontEffect.FontBatchRenderMsdfPass : fontEffect.FontBatchRenderPass);
        pass.Apply();

        GraphicsDevice.Draw(4, count);   // 4 strip verts x glyph instances
    }

    // Aggregated text batch (docs/TEXT_GLYPH_BATCH_PLAN.md §9 Stage 2): draws MANY blocks' glyphs - already baked to
    // world space, one shared atlas - in a SINGLE instanced draw straight into the main pass = the FPS win. The VS
    // applies ONLY the static Projection (positions are pre-transformed on the CPU during aggregation, the one
    // driver-safe form). The batch pixel shader takes the foreground from the per-instance vertex colour (baked per
    // glyph), so blocks of different colours share one draw. State/depth/blend match DrawLayoutDirect (main pass).
    public void DrawBatch(SamplerState samplerState, FontAtlas atlas, Buffer<FontItem> glyphs, uint glyphCount, Matrix4x4F projection, uint firstInstance = 0)
    {
        if (glyphCount == 0) return;

        GraphicsDevice.ColorBlendEnabled = true;
        GraphicsDevice.ColorBlendEquation = ColorBlendEquations.Premultiplied;
        GraphicsDevice.PrimitiveRestartEnable = true;
        GraphicsDevice.DepthTestEnabled = true;
        GraphicsDevice.DepthWriteEnable = true;
        GraphicsDevice.DepthCompareFunction = CompareOp.Always;

        effectSampler.SetResource(samplerState);
        effectTexture.SetResource(atlas.Atlas);
        effectMatrixTransform.SetValue(projection);
        effectUVCornerCoords.SetValue(UVCornerCoords);
        effectFontWeight.SetValue(FontWeight);
        effectPixelRange.SetValue(atlas.PixelRange);
        effectAtlasSize.SetValue(new Vector2F(atlas.Atlas.Width, atlas.Atlas.Height));
        effectSdfBlendLo.SetValue(SdfBlendLo);
        effectSdfBlendHi.SetValue(SdfBlendHi);
        GraphicsDevice.VertexType = vertexType;
        GraphicsDevice.SetVertexBuffer(glyphs);
        GraphicsDevice.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        // Batch pass: per-instance foreground (input.Color), so many colours in one draw. Canonical MSDF only for
        // now; outline/stroked/gradient-AA text stays on the per-block direct path (RenderCache routes it there).
        fontEffect.FontBatchRenderMsdfBatchPass.Apply();
        // firstInstance offsets the per-instance FontItem fetch to THIS segment's slice of the shared buffer (its data
        // was uploaded at the matching byte offset), so several segments share one buffer without overwriting.
        GraphicsDevice.Draw(4, glyphCount, 0, firstInstance);   // 4 strip verts x glyphCount glyph instances
    }

    // STORAGE-INSTANCED text batch: per-instance GlyphItem read from a BDA storage buffer by SV_InstanceID, and each
    // glyph's NODE-LOCAL rect transformed to world on the GPU via the transform table at its slot (slot 0 = identity).
    // Mirrors the SDF rect/ellipse fills (SdfBatchCollector) - no per-instance vertex buffer, no CPU per-glyph world bake,
    // and a scrolling block moves by one table matrix write instead of re-baking N glyphs. instancesAddress is pre-offset
    // to THIS segment's slice (drawn at base instance 0). State/depth/blend match the vertex-buffer DrawBatch above.
    public void DrawBatch(SamplerState samplerState, FontAtlas atlas, ulong instancesAddress, ulong transformsAddress,
        uint glyphCount, Matrix4x4F projection)
    {
        if (glyphCount == 0) return;

        GraphicsDevice.ColorBlendEnabled = true;
        GraphicsDevice.ColorBlendEquation = ColorBlendEquations.Premultiplied;
        GraphicsDevice.PrimitiveRestartEnable = true;
        GraphicsDevice.DepthTestEnabled = true;
        GraphicsDevice.DepthWriteEnable = true;
        GraphicsDevice.DepthCompareFunction = CompareOp.Always;

        effectSampler.SetResource(samplerState);
        effectTexture.SetResource(atlas.Atlas);
        effectMatrixTransform.SetValue(projection);
        effectUVCornerCoords.SetValue(UVCornerCoords);
        effectFontWeight.SetValue(FontWeight);
        effectPixelRange.SetValue(atlas.PixelRange);
        effectAtlasSize.SetValue(new Vector2F(atlas.Atlas.Width, atlas.Atlas.Height));
        effectSdfBlendLo.SetValue(SdfBlendLo);
        effectSdfBlendHi.SetValue(SdfBlendHi);
        effectGlyphInstances.SetValue(instancesAddress);
        effectTransforms.SetValue(transformsAddress);
        GraphicsDevice.VertexType = null;
        GraphicsDevice.PrimitiveTopology = PrimitiveTopology.TriangleStrip;
        fontEffect.FontBatchRenderMsdfBatchInstancedPass.Apply();
        GraphicsDevice.Draw(4, glyphCount, 0, 0);   // 4 strip verts x glyphCount instances; address already at this segment
    }

    public void RestoreState(bool outerPassActive = true)
    {
        if (!beginCalled)
        {
            throw new Exception("SetState must be called before end");
        }

        GraphicsDevice.EndDraw();
        var device = (GraphicsDevice)GraphicsDevice;

        device.TransitionImagesAfterRendering(device.CurrentCommandBuffer, device.CurrentRenderTarget.ResolveTexture);

        GraphicsDevice.ClearColor = _oldClearColor;
        GraphicsDevice.MSAALevel = _oldRenderTargets.MSAALevel;

        // Restore the viewport + scissor that were active when SetState interrupted the pass - NOT the full attachment.
        // Resetting to full left the resumed pass (and the text composite that immediately follows) drawing unclipped,
        // so a clipped caller (a virtualized list item) had its text poke ~half an item past the viewport edge on a
        // fast scroll. Fall back to the full attachment only if nothing was captured.
        if (_savedViewports is { Length: > 0 })
            GraphicsDevice.SetViewports(_savedViewports);
        else
            GraphicsDevice.SetViewports(new Viewport { Width = _oldRenderTargets.Width, Height = _oldRenderTargets.Height, MaxDepth = 1 });

        if (_savedScissors is { Length: > 0 })
            GraphicsDevice.SetScissors(_savedScissors);
        else
            GraphicsDevice.SetScissors(new Rect2D { Offset = new Offset2D(), Extent = new Extent2D { Width = (uint)_oldRenderTargets.Width, Height = (uint)_oldRenderTargets.Height } });

        GraphicsDevice.SetRenderTargets(_oldRenderTargets);
        GraphicsDevice.SetDepthBuffer(_oldDepthStencilBuffer);
        GraphicsDevice.DepthTestEnabled = true;
        GraphicsDevice.DepthWriteEnable = true;
        GraphicsDevice.DepthCompareFunction = CompareOp.Always;
        GraphicsDevice.ColorBlendEnabled = true;
        GraphicsDevice.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        // Inline: resume the main pass we interrupted. Pre-pass (standalone): don't - the main pass hasn't begun yet
        // (BeginDraw begins it right after PreRender), and the outer render target is already restored above.
        if (outerPassActive) GraphicsDevice.BeginRendering(GraphicsDevice.CurrentCommandBuffer, true);

        beginCalled = false;
    }
}
