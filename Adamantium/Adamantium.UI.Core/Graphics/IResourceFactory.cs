using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Core.Graphics;

public interface IResourceFactory
{
    ITexture CreateTexture(TextureDescription description, byte[] pixelData);

    /// <summary>One texture whose ARRAY LAYERS are <paramref name="layers"/> - an animation's frames, uploaded once and
    /// then selected in the shader by layer, instead of a texture per frame.</summary>
    ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers);

    /// <summary>Imports an externally produced shared surface (zero-copy) on the UI resource device.</summary>
    ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor);

    IRenderTarget CreateRenderTarget(UInt32 width,
        UInt32 height,
        MSAALevel msaa,
        SurfaceFormat format,
        ImageLayout desiredLayout);
    
    FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice);
}