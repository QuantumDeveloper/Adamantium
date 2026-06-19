using System;
using Adamantium.FX.Effects.Generated;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Imaging;
using Adamantium.Mathematics;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics.Fonts;

public class FontRenderer : GraphicsResource
{
    private FontEffect fontEffect;
    private bool beginCalled;
    private Matrix4x4F transformMatrix;
    private Type vertexType = typeof(FontItem);
    private float currentFontSize;
    private Size dotGlyphsSize;
    
    private const float FontSizeThreshold = 14;
    // Glyph weight: a signed-distance contour bias (see FontEffect.fx). 0 = exact outline, > 0 = thicker
    // stems, < 0 = thinner. Normalized units (the field spans PixelRange texels), useful range ~[-0.15, 0.15].
    // Applied inside the screenPxRange term, so it scales with size and does not haze the background.
    private float FontWeight = 0.0f;

    // Supersampling factor for text. The text render target is RenderScale x bigger than the logical text
    // area, so the ortho is divided by it (the viewport stays full size) -> glyphs rasterize RenderScale x
    // larger and get downsampled when the texture is composited onto the control = SSAA. 1 = off.
    public float RenderScale { get; set; } = 1f;

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
    private IEffectPass glyphEffectPass;

    private Vector2F currentScreenSize;
    private static readonly Vector2F[] UVCornerCoords = [Vector2F.Zero, Vector2F.UnitX, Vector2F.UnitY, Vector2F.One];
    private Matrix4x4F finalMatrix;
    private SamplerState assignedSamplerState;
    private SamplerState _oldSamplerState;
    private TextRenderingParameters renderingParameters;
    private IRenderTarget renderTarget;

    private TextLayout _textLayout;

    private Color _oldClearColor;
    private IRenderTarget _oldRenderTargets;
    private IDepthStencilBuffer _oldDepthStencilBuffer;

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
        glyphEffectPass = fontEffect.FontBatchRenderPass;
    }

    public void DrawLayout(TextLayout textLayout, Color foreground, Color stroke)
    {
        DrawInternal(textLayout, foreground, stroke);
    }

    public void DrawString(string text, string fontName, double fontSize, Rectangle textArea, TextWrapping textWrapping,
        TextTrimming textTrimming, Color color)
    {
        DrawString(text, fontName, fontSize,
            new TextRenderingParameters()
                { TextArea = textArea, Color = color, TextWrapping = textWrapping, TextTrimming = textTrimming });
    }

    public void DrawString(string text, string fontName, double fontSize, Vector2F textOrigin, Color color)
    {
        DrawString(text, fontName, fontSize,
            new TextRenderingParameters() { TextArea = new Rectangle(textOrigin, 0, 0), Color = color });
    }

    public void DrawString(string text, string fontName, double fontSize, TextRenderingParameters parameters,
        RenderTarget renderTarget = null)
    {
        renderingParameters = parameters;
        transformMatrix = Matrix4x4F.Translation(parameters.TextArea.X, parameters.TextArea.Y, 2);
        effectMatrixTransform.SetValue(transformMatrix);
    }

    public void SetState(
        SamplerState samplerState,
        Vector3F translation, 
        IRenderTarget renderTarget)
    {
        if (beginCalled)
        {
            throw new Exception("You need to call RestoreState() before you can call Begin() again");
        }

        assignedSamplerState = samplerState;
        this.renderTarget = renderTarget;
        currentScreenSize = new Vector2F(renderTarget.Width, renderTarget.Height);
        transformMatrix = Matrix4x4F.Translation(translation);
        
        GraphicsDevice.EndDraw();

        _oldClearColor = GraphicsDevice.ClearColor;
        _oldRenderTargets = GraphicsDevice.CurrentRenderTarget;
        _oldDepthStencilBuffer = GraphicsDevice.CurrentDepthStencilBuffer;

        GraphicsDevice.ClearColor = Colors.Transparent;
        GraphicsDevice.SetRenderTargets(renderTarget);
        GraphicsDevice.SetDepthBuffer(null);
        var device = (GraphicsDevice)GraphicsDevice;
        device.TransitionImagesForRendering(device.CurrentCommandBuffer, device.CurrentRenderTarget, device.CurrentRenderTarget.ResolveTexture);
        GraphicsDevice.BeginRendering(GraphicsDevice.CurrentCommandBuffer);

        // assignedSamplerState = samplerState ?? GraphicsDevice.SamplerStates.LinearFont;
        // assignedBlendState = blendState ?? GraphicsDevice.BlendStates.Fonts;
        // assignedDepthStencilState = depthStencilState ?? GraphicsDevice.DepthStencilStates.DepthEnableGreaterEqual;
        // assignedRasterizerState = rasterizerState ?? GraphicsDevice.RasterizerStates.CullNoneClipDisabled;
        //
        // oldSamplerState = GraphicsDevice.Sampler;
        // oldBlendState = GraphicsDevice.BlendState;
        // oldRasterizerState = GraphicsDevice.RasterizerState;
        // oldDepthStencilState = GraphicsDevice.DepthStencilState;

        beginCalled = true;
    }

    // private void PrepareForFlushing()
    // {
    //     var orthoProjection = Matrix4x4F.OrthoOffCenter(0, currentScreenSize.X, 0, currentScreenSize.Y, 0, 10000f);
    //     Matrix4x4F.Multiply(ref transformMatrix, ref orthoProjection, out finalMatrix);
    //
    //     if (assignedBlendState != null)
    //     {
    //         oldBlendState = GraphicsDevice.BlendState;
    //         GraphicsDevice.BlendState = assignedBlendState;
    //     }
    //
    //     if (assignedDepthStencilState != null)
    //     {
    //         oldDepthStencilState = GraphicsDevice.DepthStencilState;
    //         GraphicsDevice.DepthStencilState = assignedDepthStencilState;
    //     }
    //
    //     if (assignedRasterizerState == null) return;
    //
    //     oldRasterizerState = GraphicsDevice.RasterizerState;
    //     GraphicsDevice.RasterizerState = assignedRasterizerState;
    // }

    private void DrawInternal(TextLayout layout, Color foreground, Color stroke)
    {
        if (layout.ElementsCount == 0) return;
        
        //layout.FontAtlas.Atlas.Save("Atlas.png", ImageFileType.Png);
        
        var vp = new Viewport();
        vp.Width = currentScreenSize.X;
        vp.Height = currentScreenSize.Y;
        vp.MinDepth = 0;
        vp.MaxDepth = 1;

        var scissor = new Rect2D();
        scissor.Offset = new Offset2D();
        // scissor.Offset.X = renderingParameters.TextArea.X;
        // scissor.Offset.Y = renderingParameters.TextArea.Y;
        scissor.Extent = new Extent2D();
        // scissor.Extent.Width = (uint)renderingParameters.TextArea.Width;
        // scissor.Extent.Height = (uint)renderingParameters.TextArea.Height;
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

        //effectSampler.SetResource(GraphicsDevice.SamplerStates.LinearFont);
        effectSampler.SetResource(assignedSamplerState);
        effectTexture.SetResource(layout.FontAtlas.Atlas);
        effectMatrixTransform.SetValue(finalMatrix);
        effectUVCornerCoords.SetValue(UVCornerCoords);
        effectForegroundColor.SetValue(foreground.ToVector4());
        effectFontSize.SetValue(layout.FontSize);
        effectFontSizeThreshold.SetValue(FontSizeThreshold);
        effectFontWeight.SetValue(FontWeight);
        effectPixelRange.SetValue(layout.FontAtlas.PixelRange);
        effectAtlasSize.SetValue(new Vector2F(layout.FontAtlas.Atlas.Width, layout.FontAtlas.Atlas.Height));
        effectOutlineColor.SetValue(OutlineColor.ToVector4());
        effectOutlineWidth.SetValue(OutlineWidth);
        effectSdfBlendLo.SetValue(SdfBlendLo);
        effectSdfBlendHi.SetValue(SdfBlendHi);
        GraphicsDevice.VertexType = vertexType;
        GraphicsDevice.SetVertexBuffer(layout.VertexBuffer);
        GraphicsDevice.PrimitiveTopology = PrimitiveTopology.PointList;
        //GraphicsDevice.DepthTestEnabled = true;
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
        
        GraphicsDevice.Draw(layout.ElementsCount, 1);
        //glyphEffectPass.UnApply(true);
    }

    public void RestoreState()
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
        var viewport = new Viewport() {Width = _oldRenderTargets.Width, Height = _oldRenderTargets.Height, MaxDepth = 1};
        GraphicsDevice.SetViewports(viewport);
        var scissor = new Rect2D();
        scissor.Offset = new Offset2D();
        // scissor.Offset.X = renderingParameters.TextArea.X;
        // scissor.Offset.Y = renderingParameters.TextArea.Y;
        scissor.Extent = new Extent2D();
        // scissor.Extent.Width = (uint)renderingParameters.TextArea.Width;
        // scissor.Extent.Height = (uint)renderingParameters.TextArea.Height;
        scissor.Extent.Width = (uint)viewport.Width;
        scissor.Extent.Height = (uint)viewport.Height;
        GraphicsDevice.SetScissors(scissor);
        GraphicsDevice.SetRenderTargets(_oldRenderTargets);
        GraphicsDevice.SetDepthBuffer(_oldDepthStencilBuffer);
        GraphicsDevice.DepthTestEnabled = true;
        GraphicsDevice.DepthWriteEnable = true;
        GraphicsDevice.DepthCompareFunction = CompareOp.Always;
        GraphicsDevice.ColorBlendEnabled = true;
        GraphicsDevice.ColorBlendEquation = ColorBlendEquations.AlphaBlend;
        GraphicsDevice.BeginRendering(GraphicsDevice.CurrentCommandBuffer, true);

        // if (oldSamplerState != null)
        // {
        //     GraphicsDevice.Sampler = oldSamplerState;
        // }
        //
        // if (oldRasterizerState != null)
        // {
        //     GraphicsDevice.RasterizerState = oldRasterizerState;
        // }
        //
        // if (oldBlendState != null)
        // {
        //     GraphicsDevice.BlendState = oldBlendState;
        // }
        //
        // if (oldDepthStencilState != null)
        // {
        //     GraphicsDevice.DepthStencilState = oldDepthStencilState;
        // }
        
        // GraphicsDevice.SetRenderTarget(null);
        // GraphicsDevice.SetDepthBuffer(null);
    
        beginCalled = false;
    }
}