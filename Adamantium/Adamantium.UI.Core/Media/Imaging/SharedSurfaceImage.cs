using System;
using Adamantium.Graphics.Core;
using Adamantium.UI.Core.Graphics;

namespace Adamantium.UI.Core.Media.Imaging;

/// <summary>
/// An <see cref="ImageSource"/> backed by an externally produced shared surface. The surface is imported
/// zero-copy on first render (on the UI resource device, via the factory) and then sampled like any bitmap.
/// Disposing frees the imported texture and, through it, the Vulkan import and its OS handles.
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

        return Texture ??= factory.ImportSharedSurface(_descriptor);
    }

    // The imported surface's GPU lifetime is owned by the ImageRenderComponent that samples it - it frees the import
    // through the render device's fence-gated deferred-dispose when the compositor switches off it (a producer resize
    // hands over a new surface). So this source must NOT dispose the texture: freeing it on the panel's / GC finalizer
    // thread would race the render thread still replaying an op that samples it, or double-free what the component
    // already released. Drop the reference only.
    protected override void ReleaseUnmanagedResources()
    {
        Texture = null;
    }
}
