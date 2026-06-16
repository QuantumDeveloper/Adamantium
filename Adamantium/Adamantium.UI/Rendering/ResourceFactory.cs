using System;
using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.UI.Core.Graphics;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Rendering;

public class ResourceFactory : IResourceFactory
{
    private readonly IGraphicsDeviceService _graphicsDeviceService;
    private readonly Dictionary<IGraphicsDevice, FontRenderer> _fontRenderers = new();

    public ResourceFactory(IGraphicsDeviceService graphicsDeviceService)
    {
        _graphicsDeviceService = graphicsDeviceService;
    }
    
    public ITexture CreateTexture(TextureDescription description, byte[] pixelData)
    {
        return _graphicsDeviceService.ResourceLoaderDevice.CreateTexture(description, pixelData);
    }

    public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor)
    {
        return _graphicsDeviceService.ResourceLoaderDevice.ImportSharedSurface(descriptor);
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
        // One FontRenderer per device, reused (rendering is sequential, so sharing is safe).
        if (!_fontRenderers.TryGetValue(graphicsDevice, out var renderer))
        {
            renderer = new FontRenderer(graphicsDevice);
            _fontRenderers[graphicsDevice] = renderer;
        }

        return renderer;
    }
}