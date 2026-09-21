using System;
using System.Collections.Generic;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Fonts;
using Adamantium.Imaging;
using Adamantium.Vulkan.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UITests.Rendering;

/// <summary>A factory that makes REAL textures on the test device - the stubs elsewhere refuse, because nothing else in
/// the suite samples one. Shared by every fixture that draws a textured brush.</summary>
internal sealed class DeviceResourceFactory : IResourceFactory
{
    private readonly IGraphicsDevice _device;

    public DeviceResourceFactory(IGraphicsDevice device) => _device = device;

    public ITexture CreateTexture(TextureDescription description, byte[] pixelData)
        => _device.CreateTexture(description, pixelData);

    public ITexture CreateTextureArray(TextureDescription description, IReadOnlyList<byte[]> layers)
        => _device.CreateTextureArray(description, layers);

    public ITexture ImportSharedSurface(SharedSurfaceDescriptor descriptor) => throw new NotSupportedException();

    public IRenderTarget CreateRenderTarget(uint width, uint height, MSAALevel msaa, SurfaceFormat format, ImageLayout desiredLayout)
        => throw new NotSupportedException();

    public FontRenderer GetFontRenderer(IGraphicsDevice graphicsDevice) => throw new NotSupportedException();
}
