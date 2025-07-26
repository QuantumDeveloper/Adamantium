using System;
using Adamantium.FX.Effects.Generated;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.EffectsFramework;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

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
    private float FontSharpness = 5;

    private EffectParameter effectSampler;
    private EffectParameter effectTexture;
    private EffectParameter effectMatrixTransform;
    private EffectParameter effectUVCornerCoords;
    private EffectParameter effectForegroundColor;
    private EffectParameter effectFontSize;
    private EffectParameter effectFontSizeThreshold;
    private EffectParameter effectFontSharpness;
    private EffectParameter effectPixelRange;
    private EffectParameter effectAtlasSize;
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
        effectFontSharpness = fontEffect.FontSharpness;
        effectPixelRange = fontEffect.PxRange;
        effectAtlasSize = fontEffect.MSDFAtlasSize;
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
        
        // layout.FontAtlas.Atlas.Save("Atlas.png", ImageFileType.Png);
        
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
        
        var orthoProjection = Matrix4x4F.OrthoOffCenter(0, currentScreenSize.X, 0, currentScreenSize.Y, 0f, 100000f);
        finalMatrix = transformMatrix * orthoProjection;
        GraphicsDevice.SetViewports(vp);
        GraphicsDevice.SetScissors(scissor);
        GraphicsDevice.ColorBlendEnabled = true;
        GraphicsDevice.ColorBlendEquation = ColorBlendEquations.Fonts;
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
        effectFontSharpness.SetValue(FontSharpness);
        effectPixelRange.SetValue(layout.FontAtlas.PixelRange);
        effectAtlasSize.SetValue(new Vector2F(layout.FontAtlas.Atlas.Width, layout.FontAtlas.Atlas.Height));
        GraphicsDevice.VertexType = vertexType;
        GraphicsDevice.SetVertexBuffer(layout.VertexBuffer);
        GraphicsDevice.PrimitiveTopology = PrimitiveTopology.PointList;
        //GraphicsDevice.DepthTestEnabled = true;
        if (stroke == Colors.Transparent)
        {
            //glyphEffectPass.Apply();
            fontEffect.FontBatchRenderPass.Apply();
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