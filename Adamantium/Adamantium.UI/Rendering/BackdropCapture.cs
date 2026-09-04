using System;
using Adamantium.Graphics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
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
    // How much smaller the copy is than the region it came from, for a material that BLURS it. A quarter in each axis
    // is a 16th of the pixels and is already a visible blur, so the frosted pass gets a wide, cheap blur out of very few
    // taps - the shrink is doing most of the work for it.
    //
    // NOT FOR GLASS, and this used to say it was. The refracting pass samples the copy SHARPLY and displaces it, so
    // whatever detail the shrink threw away is detail the lens has nothing left to bend: the copy's resolution is the
    // material's whole detail budget (see the notes on the capture region). Asked for at a quarter size, liquid glass
    // came out looking like frosted plastic no matter how strong its refraction was - correctly, because it was bending
    // an image that had no detail finer than four pixels in it to begin with. Which resolution a material wants is now
    // the material's own answer; see Sharp.
    public const int Downscale = 4;

    /// <summary>What a material that BENDS the copy asks for instead: no shrink at all. The copy is of the element's own
    /// region plus a margin - a menu or a panel, not the window - so a full-resolution blit of it is one small transfer,
    /// and it is the only way the lens has anything to bend.</summary>
    public const int Sharp = 1;

    // ONE TEXTURE PER FRAME IN FLIGHT, not one texture. The capture is written by a blit and read by a shader in the
    // SAME frame, so a single image is written by frame N while frame N-1 is still sampling it - a write-after-read the
    // barriers inside one command buffer say nothing about, and the way this shows up is the GPU dying with the
    // validation layer silent. Indexed by the device's current frame, exactly as ReusableBuffer's ring is.
    private Texture[] _ring;
    private Texture _current;
    private uint _width, _height;

    /// <summary>The last captured image, or null if nothing has been captured yet. Bound by the material pass.</summary>
    public ITexture Image => _current;

    /// <summary>Where the capture came from, in DEVICE pixels - the material's pixel shader needs it to map a fragment
    /// back into the copy.</summary>
    public Rect2D Region { get; private set; }

    /// <summary>Copy the frame region behind an element into this capture, breaking the render pass open around the
    /// transfer and re-opening it afterwards. Returns false when there is nothing to copy (no target, empty region) -
    /// the caller then draws the element without a backdrop rather than with a stale one.</summary>
    public bool Capture(IGraphicsDevice device, Rect2D region, int downscale = Downscale)
    {
        if (device is not GraphicsDevice gd) return false;

        downscale = Math.Max(1, downscale);

        var source = gd.CurrentRenderTarget?.ResolveTexture;
        if (source == null) return false;

        // Clamp to the target: a region grown by the blur margin can hang off the edge, and a blit past the source
        // bounds is a GPU fault, not a clipped copy.
        var x = (int)Math.Clamp(region.Offset.X, 0, (int)source.Width);
        var y = (int)Math.Clamp(region.Offset.Y, 0, (int)source.Height);
        var right = (int)Math.Clamp(region.Offset.X + region.Extent.Width, 0, source.Width);
        var bottom = (int)Math.Clamp(region.Offset.Y + region.Extent.Height, 0, source.Height);
        if (right - x < downscale || bottom - y < downscale) return false;

        var w = (uint)Math.Max(1, (right - x) / downscale);
        var h = (uint)Math.Max(1, (bottom - y) / downscale);
        EnsureTexture(gd, w, h);
        _current = _ring[gd.CurrentFrame % (uint)_ring.Length];
        if (_current == null) return false;

        Region = new Rect2D
        {
            Offset = new Offset2D { X = x, Y = y },
            Extent = new Extent2D { Width = (uint)(right - x), Height = (uint)(bottom - y) }
        };

        var commandBuffer = gd.CurrentCommandBuffer;
        gd.SuspendRendering();

        // The barriers below move the images on the GPU; these two lines move what the texture OBJECTS believe about
        // themselves, so the next thing to transition either of them starts from the state it is actually in.
        //
        // ASSIGNED, not transitioned: TransitionImageLayout opens a SINGLE-TIME command buffer of its own, and doing
        // that in the middle of recording the frame's buffer crashes the process outright - which is exactly what it
        // did here. The existing CopyImage path can call it because it runs on its own buffer to begin with.
        source.ImageLayout = ImageLayout.TransferSrcOptimal;
        _current.ImageLayout = ImageLayout.TransferDstOptimal;

        gd.InsertImageMemoryBarrier(commandBuffer, source,
            AccessFlagBits.ColorAttachmentWriteBit, AccessFlagBits.TransferReadBit,
            ImageLayout.ColorAttachmentOptimal, ImageLayout.TransferSrcOptimal,
            PipelineStageFlagBits.ColorAttachmentOutputBit, PipelineStageFlagBits.TransferBit);

        gd.InsertImageMemoryBarrier(commandBuffer, _current,
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
            _current.GetImage(), ImageLayout.TransferDstOptimal, 1, blit, Filter.Linear);

        gd.InsertImageMemoryBarrier(commandBuffer, _current,
            AccessFlagBits.TransferWriteBit, AccessFlagBits.ShaderReadBit,
            ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlagBits.TransferBit, PipelineStageFlagBits.FragmentShaderBit);

        gd.InsertImageMemoryBarrier(commandBuffer, source,
            AccessFlagBits.TransferReadBit, AccessFlagBits.ColorAttachmentWriteBit,
            ImageLayout.TransferSrcOptimal, ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlagBits.TransferBit, PipelineStageFlagBits.ColorAttachmentOutputBit);

        source.ImageLayout = ImageLayout.ColorAttachmentOptimal;
        _current.ImageLayout = ImageLayout.ShaderReadOnlyOptimal;

        gd.ResumeRendering();
        return true;
    }

    // Kept between captures and re-made only when the size changes - the same rule the off-screen renderer's target
    // follows, and for the same reason: a fresh image per capture exhausts device memory in seconds when something
    // behind the material moves every frame.
    //
    // The old images go to the DEFERRED queue, never to Dispose: the element only has to change size by a pixel - a
    // scroll, a resize - for this to run while earlier frames are still sampling what it is about to free, and freeing
    // a texture out from under an in-flight frame kills the device with nothing in the validation log.
    private void EnsureTexture(GraphicsDevice device, uint width, uint height)
    {
        _ring ??= new Texture[Math.Max(1, (int)device.MaxFramesInFlight)];
        if (_ring[0] != null && _width == width && _height == height) return;

        for (var i = 0; i < _ring.Length; i++)
        {
            if (_ring[i] != null) device.AddToDeferDisposeQueue(_ring[i]);
            _ring[i] = null;
        }

        _current = null;
        _width = width;
        _height = height;
        for (var i = 0; i < _ring.Length; i++)
        {
            _ring[i] = Graphics.Texture.New(device, new TextureDescription
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
            }, $"BackdropCapture:{i}");
        }
    }

    public void Dispose()
    {
        if (_ring == null) return;

        for (var i = 0; i < _ring.Length; i++)
        {
            _ring[i]?.Dispose();
            _ring[i] = null;
        }

        _current = null;
    }
}
