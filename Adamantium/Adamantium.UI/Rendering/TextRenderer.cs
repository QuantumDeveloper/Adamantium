using System;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Graphics.Fonts;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

internal class TextRenderer : GeometryRenderer
{
    public TextRenderer(
        GraphicsDevice device, 
        Geometry geometry, 
        TextLayout textLayout, 
        TextRenderingParameters renderingParameters, 
        Brush background, 
        Brush foreground) : 
        base(device, 
            geometry, 
            background, 
            foreground)
    {
        TextLayout = textLayout;
        TextRenderingParameters = renderingParameters;
        var renderTargetParams = PresentationParameters.RenderTargetParameters(
            (uint)geometry.Bounds.Width, 
            (uint)geometry.Bounds.Height,
            MSAALevel.X4);
        _renderToTextureDevice = device.MainDevice.CreateRenderDevice(renderTargetParams);
        _renderToTextureDevice.AddDynamicStates(DynamicState.Viewport, DynamicState.Scissor);
        FontRenderer = new FontRenderer(_renderToTextureDevice);
    }

    private GraphicsDevice _renderToTextureDevice;
    
    public TextRenderingParameters TextRenderingParameters { get; }
    
    public TextLayout TextLayout { get; }
    
    public FontRenderer FontRenderer { get; }

    public override bool PrepareFrame(GraphicsDevice graphicsDevice, IUIComponent component, ImageSource image,
        Matrix4x4F projectionMatrix)
    {
        return true;
    }

    public override void Draw(GraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix)
    {
        Draw(graphicsDevice, component, null, projectionMatrix);
    }

    public override void Draw(GraphicsDevice graphicsDevice, IUIComponent component, ImageSource image, Matrix4x4F projectionMatrix)
    {
        var textBlock = (TextBlock)component;
        
        var location = new Vector3F(TextRenderingParameters.TextArea.X,
            TextRenderingParameters.TextArea.Y, 
            5);
        
        var resolveTexture = ((RenderTargetGraphicsPresenter)_renderToTextureDevice.Presenter).ResolveTexture;
        resolveTexture.TransitionImageLayout(ImageLayout.ColorAttachmentOptimal);

        var foreground = ((SolidColorBrush)Foreground).Color;
        var stroke = ((SolidColorBrush)textBlock.Stroke).Color;
        _renderToTextureDevice.ClearColor = ((SolidColorBrush)Background).Color;
        _renderToTextureDevice.BeginDraw(1, 0);
        FontRenderer.SetState(null, null, null, null, location, _renderToTextureDevice.Presenter.RenderTarget);
        FontRenderer.DrawLayout(TextLayout, foreground, stroke);
        FontRenderer.RestoreState();
        _renderToTextureDevice.EndDraw();
        _renderToTextureDevice.Submit();

        Texture = resolveTexture;
        resolveTexture.TransitionImageLayout(ImageLayout.ShaderReadOnlyOptimal);
        base.Draw(graphicsDevice, component, image, projectionMatrix);
    }
}