using System;
using Adamantium.Graphics.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Core.Media.Imaging;

/// <summary>
/// An <see cref="ImageSource"/> backed by an externally produced shared surface, imported zero-copy on first render
/// and then sampled like any bitmap.
/// </summary>
public sealed class SharedSurfaceImage : BitmapSource
{
    private readonly SharedSurfaceDescriptor _descriptor;

    public SharedSurfaceImage(SharedSurfaceDescriptor descriptor)
    {
        _descriptor = descriptor;
        PixelWidth = descriptor.Width;
        PixelHeight = descriptor.Height;
        SurfaceLayout = descriptor.Format;
    }

    public SharedSurfaceDescriptor Descriptor => _descriptor;

    public override ITexture GetOrCreateTexture(IResourceFactory factory)
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(SharedSurfaceImage));
        }

        // The import died with its device, and so did the producer's surface: nothing to import again until the
        // producer hands over a surface made on the new device.
        if (Texture is { IsDisposed: true })
        {
            return null;
        }

        return Texture ??= factory.ImportSharedSurface(_descriptor);
    }

    // The render component that samples the import owns and frees it; disposing here would race the render thread.
    protected override void ReleaseUnmanagedResources()
    {
        Texture = null;
    }
}
