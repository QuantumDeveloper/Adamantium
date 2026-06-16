using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.UI.Core.Graphics;

public interface IResourceFactory
{
    ITexture CreateTexture(TextureDescription description, byte[] pixelData);

    /// <summary>Imports an externally produced shared surface (zero-copy) on the UI resource device.</summary>
    ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor);

    IRenderTarget CreateRenderTarget(UInt32 width,
        UInt32 height,
        MSAALevel msaa,
        SurfaceFormat format,
        ImageLayout desiredLayout);
    
    FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice);
}