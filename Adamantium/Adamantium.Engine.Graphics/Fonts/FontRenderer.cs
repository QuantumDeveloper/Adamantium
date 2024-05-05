using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Engine.Graphics.Effects;
using Adamantium.Engine.Graphics.Effects.Generated;
using Adamantium.Fonts;
using Adamantium.Fonts.TextureGeneration;
using Adamantium.Imaging;
using Adamantium.Imaging.PaletteQuantizer.Extensions;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics.Fonts;

public class FontRenderer : GraphicsResource
{
    private FontEffect fontEffect;
    private bool beginCalled;
    private Matrix4x4F transformMatrix;
    private Type vertexType = typeof(FontItem);
    private float currentFontSize;
    private Size dotGlyphsSize;
    
    private const float FontSizeThreshold = 24;
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
    private EffectPass glyphEffectPass;

    private Vector2F currentScreenSize;
    private static readonly Vector2F[] UVCornerCoords = [Vector2F.Zero, Vector2F.UnitX, Vector2F.UnitY, Vector2F.One];
    private Matrix4x4F finalMatrix;
    private SamplerState assignedSamplerState;
    private BlendState assignedBlendState;
    private DepthStencilState assignedDepthStencilState;
    private RasterizerState assignedRasterizerState;
    private SamplerState oldSamplerState;
    private BlendState oldBlendState;
    private DepthStencilState oldDepthStencilState;
    private RasterizerState oldRasterizerState;
    private TextRenderingParameters renderingParameters;
    private RenderTarget renderTarget;

    private TextLayout _textLayout;

    public FontRenderer(GraphicsDevice device) : base(device)
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
        effectPixelRange = fontEffect.PXRange;
        glyphEffectPass = fontEffect.FontBatchRenderPass;
    }

    public void DrawLayout(TextLayout textLayout, Color foreground)
    {
        DrawInternal(textLayout, foreground);
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
        //DrawInternal();
    }

    public void SetState(
        BlendState blendState,
        SamplerState samplerState,
        DepthStencilState depthStencilState,
        RasterizerState rasterizerState,
        Vector3F translation, 
        RenderTarget renderTarget)
    {
        if (beginCalled)
        {
            throw new Exception("You need to call RestoreState() before you can call Begin() again");
        }

        this.renderTarget = renderTarget;
        currentScreenSize = new Vector2F(renderTarget.Width, renderTarget.Height);
        transformMatrix = Matrix4x4F.Translation(translation);

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

    private void DrawInternal(TextLayout layout, Color foreground)
    {
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
        Matrix4x4F.Multiply(ref transformMatrix, ref orthoProjection, out finalMatrix);
        GraphicsDevice.SetViewports(vp);
        GraphicsDevice.SetScissors(scissor);
        GraphicsDevice.SetRenderTarget(renderTarget);
        GraphicsDevice.DepthStencilState = GraphicsDevice.DepthStencilStates.DepthEnableLessEqual;
        GraphicsDevice.BlendState = GraphicsDevice.BlendStates.Fonts;

        effectSampler.SetResource(GraphicsDevice.SamplerStates.LinearFont);
        effectTexture.SetResource(layout.FontAtlas.Atlas);
        effectMatrixTransform.SetValue(finalMatrix);
        effectUVCornerCoords.SetValue(UVCornerCoords);
        effectForegroundColor.SetValue(foreground.ToVector4());
        effectFontSize.SetValue(layout.FontSize);
        effectFontSizeThreshold.SetValue(FontSizeThreshold);
        effectFontSharpness.SetValue(FontSharpness);
        effectPixelRange.SetValue(layout.FontAtlas.PixelRange);
        GraphicsDevice.VertexType = vertexType;
        GraphicsDevice.SetVertexBuffer(layout.VertexBuffer);
        GraphicsDevice.PrimitiveTopology = PrimitiveTopology.PointList;
        glyphEffectPass.Apply();
        GraphicsDevice.Draw(layout.ElementsCount, 1, 0);
        glyphEffectPass.UnApply(true);
    }

    public void RestoreState()
    {
        if (!beginCalled)
        {
            throw new Exception("SetState must be called before end");
        }

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