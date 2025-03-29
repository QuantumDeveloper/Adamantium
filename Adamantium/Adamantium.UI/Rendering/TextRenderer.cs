using Adamantium.FX.Effects.Generated;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.UI.Controls;
using Adamantium.UI.Media;
using Adamantium.UI.Media.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

internal class TextRenderer : GeometryRenderer
{
    public TextRenderer(
        IGraphicsDevice device, 
        Geometry geometry, 
        TextLayout textLayout, 
        TextRenderingParameters renderingParameters, 
        Brush background, 
        Brush foreground,
        BasicEffect basicEffect) : 
        base(device, 
            geometry, 
            background, 
            foreground,
            basicEffect)
    {
        TextLayout = textLayout;
        TextRenderingParameters = renderingParameters;
        _renderToTextureDevice = (GraphicsDevice)device.MainDevice.CreateRenderDevice();
        renderTarget = RenderTarget.New(_renderToTextureDevice, 
            (uint)geometry.Bounds.Width,
            (uint)geometry.Bounds.Height,
            MSAALevel.X4, 
            SurfaceFormat.R8G8B8A8.UNorm,
            name:"TextRenderer");
        FontRenderer = new FontRenderer(_renderToTextureDevice);
    }

    private GraphicsDevice _renderToTextureDevice;

    private RenderTarget renderTarget;
    
    public TextRenderingParameters TextRenderingParameters { get; }
    
    public TextLayout TextLayout { get; }
    
    public FontRenderer FontRenderer { get; }

    public override bool PrepareFrame(IGraphicsDevice graphicsDevice, IUIComponent component, ImageSource image,
        Matrix4x4F projectionMatrix)
    {
        return true;
    }

    public override void Draw(IGraphicsDevice graphicsDevice, IUIComponent component, Matrix4x4F projectionMatrix)
    {
        Draw(graphicsDevice, component, null, projectionMatrix);
    }

    public override void Draw(IGraphicsDevice graphicsDevice, IUIComponent component, ImageSource image, Matrix4x4F projectionMatrix)
    {
        var textBlock = (TextBlock)component;
        
        var location = new Vector3F(TextRenderingParameters.TextArea.X,
            TextRenderingParameters.TextArea.Y, 
            5);
        
        var resolveTexture = renderTarget.ResolveTexture;
        resolveTexture.TransitionImageLayout(ImageLayout.ColorAttachmentOptimal);

        var foreground = ((SolidColorBrush)Foreground).Color;
        var stroke = ((SolidColorBrush)textBlock.Stroke).Color;
        _renderToTextureDevice.ClearColor = ((SolidColorBrush)Background).Color;
        _renderToTextureDevice.MSAALevel = renderTarget.MSAALevel;
        //_renderToTextureDevice.BeginDraw(1, 0);
        FontRenderer.SetState(null, location, renderTarget);
        FontRenderer.DrawLayout(TextLayout, foreground, stroke);
        FontRenderer.RestoreState();
        _renderToTextureDevice.EndDraw();
        _renderToTextureDevice.Submit();

        Texture = resolveTexture;
        resolveTexture.TransitionImageLayout(ImageLayout.ShaderReadOnlyOptimal);
        base.Draw(graphicsDevice, component, image, projectionMatrix);
    }
}