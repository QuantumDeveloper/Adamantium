using System;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.UI.Core.Graphics;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

public class ResourceFactory : IResourceFactory
{
    private readonly IGraphicsDeviceService _graphicsDeviceService;
    
    public ResourceFactory(IGraphicsDeviceService graphicsDeviceService)
    {
        _graphicsDeviceService = graphicsDeviceService;
    }
    
    public ITexture CreateTexture(TextureDescription description, byte[] pixelData)
    {
        return _graphicsDeviceService.ResourceLoaderDevice.CreateTexture(description, pixelData);
    }

    public IRenderTarget CreateRenderTarget(UInt32 width, 
        UInt32 height, 
        MSAALevel msaa, 
        SurfaceFormat format, 
        ImageLayout desiredLayout)
    {
        return _graphicsDeviceService.ResourceLoaderDevice.CreateRenderTarget(width, height, msaa, format, ImageUsageFlagBits.TransferDstBit, desiredLayout);
    }

    public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice)
    {
        return new FontRenderer(graphicsDevice);
    }
}