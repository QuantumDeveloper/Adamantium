using System;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Mathematics;
using Adamantium.Imaging;
using Adamantium.Vulkan.Core;

namespace Adamantium.UI.Rendering;

/// <summary>
/// What the backdrop materials read: a copy of the region of the frame ALREADY DRAWN behind an element.
///
/// <para>Not a re-render. <c>VisualRenderer</c> exists for the other question - draw this subtree somewhere else - and it
/// answers it by RUNNING the subtree again. A material needs the opposite: whatever happens to be behind the element,
/// including things it knows nothing about, exactly as composited. That is a transfer out of the colour target, not a
/// draw, which is why it needs the pass broken open (SuspendRendering/ResumeRendering).</para>
///
/// <para>DOWNSCALING IS THE FIRST BLUR PASS, not an optimisation bolted on. The blit is filtered, so copying the region
/// into a quarter-size image already averages 4x4 neighbourhoods for free; the shader then samples that with a linear
/// sampler and gets a wide, cheap blur out of very few taps. Doing it at full size would cost the copy AND a real
/// convolution.</para>
///
/// <para>The region is the element's box GROWN by a margin, because a blur reaches outside what it covers: sample right
/// up to the edge and the border of the material darkens towards whatever the clamp returns.</para>
/// </summary>
internal sealed class BackdropCapture : IDisposable
{
    // How much smaller the copy is than the region it came from. 4 is the useful compromise: a quarter in each axis is
    // a 16th of the pixels and already a visible blur, while staying sharp enough that a LiquidGlass refraction reads
    // as glass rather than as fog.
    public const int Downscale = 4;

    private Texture _texture;
    private uint _width, _height;

    /// <summary>The last captured image, or null if nothing has been captured yet. Bound by the material pass.</summary>
    public ITexture Image => _texture;

    /// <summary>Where the capture came from, in DEVICE pixels - the material's pixel shader needs it to map a fragment
    /// back into the copy.</summary>
    public Rect2D Region { get; private set; }

    /// <summary>Copy the frame region behind an element into this capture, breaking the render pass open around the
    /// transfer and re-opening it afterwards. Returns false when there is nothing to copy (no target, empty region) -
    /// the caller then draws the element without a backdrop rather than with a stale one.</summary>
    public bool Capture(IGraphicsDevice device, Rect2D region)
    {
        if (device is not GraphicsDevice gd) return false;

        var source = gd.CurrentRenderTarget?.ResolveTexture;
        if (source == null) return false;

        // Clamp to the target: a region grown by the blur margin can hang off the edge, and a blit past the source
        // bounds is a GPU fault, not a clipped copy.
        var x = (int)Math.Clamp(region.Offset.X, 0, (int)source.Width);
        var y = (int)Math.Clamp(region.Offset.Y, 0, (int)source.Height);
        var right = (int)Math.Clamp(region.Offset.X + region.Extent.Width, 0, source.Width);
        var bottom = (int)Math.Clamp(region.Offset.Y + region.Extent.Height, 0, source.Height);
        if (right - x < Downscale || bottom - y < Downscale) return false;

        var w = (uint)Math.Max(1, (right - x) / Downscale);
        var h = (uint)Math.Max(1, (bottom - y) / Downscale);
        EnsureTexture(gd, w, h);
        if (_texture == null) return false;

        Region = new Rect2D
        {
            Offset = new Offset2D { X = x, Y = y },
            Extent = new Extent2D { Width = (uint)(right - x), Height = (uint)(bottom - y) }
        };

        var commandBuffer = gd.CurrentCommandBuffer;
        gd.SuspendRendering();

        gd.InsertImageMemoryBarrier(commandBuffer, source,
            AccessFlagBits.ColorAttachmentWriteBit, AccessFlagBits.TransferReadBit,
            ImageLayout.ColorAttachmentOptimal, ImageLayout.TransferSrcOptimal,
            PipelineStageFlagBits.ColorAttachmentOutputBit, PipelineStageFlagBits.TransferBit);

        gd.InsertImageMemoryBarrier(commandBuffer, _texture,
            AccessFlagBits.ShaderReadBit, AccessFlagBits.TransferWriteBit,
            ImageLayout.ShaderReadOnlyOptimal, ImageLayout.TransferDstOptimal,
            PipelineStageFlagBits.FragmentShaderBit, PipelineStageFlagBits.TransferBit);

        var blit = new ImageBlit
        {
            SrcSubresource = new ImageSubresourceLayers { AspectMask = ImageAspectFlagBits.ColorBit, LayerCount = 1 },
            DstSubresource = new ImageSubresourceLayers { AspectMask = ImageAspectFlagBits.ColorBit, LayerCount = 1 },
            SrcOffsets = new[]
            {
                new Offset3D { X = x, Y = y, Z = 0 },
                new Offset3D { X = right, Y = bottom, Z = 1 }
            },
            DstOffsets = new[]
            {
                new Offset3D { X = 0, Y = 0, Z = 0 },
                new Offset3D { X = (int)w, Y = (int)h, Z = 1 }
            }
        };

        // Linear, not Nearest: the filtering IS the first blur pass (see the note above).
        commandBuffer.BlitImage(source.GetImage(), ImageLayout.TransferSrcOptimal,
            _texture.GetImage(), ImageLayout.TransferDstOptimal, 1, blit, Filter.Linear);

        gd.InsertImageMemoryBarrier(commandBuffer, _texture,
            AccessFlagBits.TransferWriteBit, AccessFlagBits.ShaderReadBit,
            ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlagBits.TransferBit, PipelineStageFlagBits.FragmentShaderBit);

        gd.InsertImageMemoryBarrier(commandBuffer, source,
            AccessFlagBits.TransferReadBit, AccessFlagBits.ColorAttachmentWriteBit,
            ImageLayout.TransferSrcOptimal, ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlagBits.TransferBit, PipelineStageFlagBits.ColorAttachmentOutputBit);

        gd.ResumeRendering();
        return true;
    }

    // Kept between captures and re-made only when the size changes - the same rule the off-screen renderer's target
    // follows, and for the same reason: a fresh image per capture exhausts device memory in seconds when something
    // behind the material moves every frame.
    private void EnsureTexture(GraphicsDevice device, uint width, uint height)
    {
        if (_texture != null && _width == width && _height == height) return;

        _texture?.Dispose();
        _width = width;
        _height = height;
        _texture = Graphics.Texture.New(device, new TextureDescription
        {
            Width = width,
            Height = height,
            Depth = 1,
            ArrayLayers = 1,
            MipLevels = 1,
            Samples = MSAALevel.None,
            Format = Format.R8G8B8A8_UNORM,
            InitialLayout = ImageLayout.Undefined,
            DesiredImageLayout = ImageLayout.ShaderReadOnlyOptimal,
            ImageType = ImageType._2d,
            ImageAspect = ImageAspectFlagBits.ColorBit,
            ImageTiling = Vulkan.Core.ImageTiling.Optimal,
            Usage = ImageUsageFlagBits.SampledBit | ImageUsageFlagBits.TransferDstBit,
            Dimension = TextureDimension.Texture2D
        }, "BackdropCapture");
    }

    public void Dispose()
    {
        _texture?.Dispose();
        _texture = null;
    }
}
