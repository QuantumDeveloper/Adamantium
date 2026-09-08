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

        // ROUNDED DOWN to a multiple of 2^halvings, so every level of the pyramid is exactly half of the one above it.
        // The REGION is left alone on purpose: the blit scales whatever rectangle it is given into whatever size the
        // copy is, so the copy still covers exactly the region and the material's mapping stays true. Shrinking the
        // region to match was tried and is wrong twice over - it cuts pixels off the area the element actually sits in,
        // and what showed through the pane then no longer lined up with what was beside it.
        var align = 1u << Halvings(w, h);
        w -= w % align;
        h -= h % align;
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

        // Leaves EVERY level, level 0 included, in ShaderReadOnly - so nothing more is owed here.
        BuildPyramid(gd, commandBuffer, _current, (int)w, (int)h);

        gd.InsertImageMemoryBarrier(commandBuffer, source,
            AccessFlagBits.TransferReadBit, AccessFlagBits.ColorAttachmentWriteBit,
            ImageLayout.TransferSrcOptimal, ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlagBits.TransferBit, PipelineStageFlagBits.ColorAttachmentOutputBit);

        source.ImageLayout = ImageLayout.ColorAttachmentOptimal;
        _current.ImageLayout = ImageLayout.ShaderReadOnlyOptimal;

        gd.ResumeRendering();
        return true;
    }

    /// <summary>The smallest a level is allowed to get. Below this a level stops being a blur of the picture and
    /// becomes an average of the whole thing.</summary>
    private const uint SmallestLevel = 8;

    /// <summary>The most halvings a copy will carry: level 6 is a blur 64 device pixels wide, past anything a surface
    /// asks for.</summary>
    private const int MaxHalvings = 6;

    /// <summary>How many times this size can be halved EXACTLY while every level stays usable - and therefore what the
    /// copy's size has to be a multiple of.
    /// <para>The height matters as much as the filter: a level that does not exist cannot be sampled, so a radius past
    /// the top of the pyramid is CLAMPED to it. That clamp is in device pixels, so the same brush covered the same
    /// number of pixels on a 100% and a 150% display - and since the pane is half again as big on the second, the blur
    /// read as weaker there. A taller pyramid is what makes the radius mean the same thing on both.</para></summary>
    private static int Halvings(uint width, uint height)
    {
        var smaller = Math.Min(width, height);
        var halvings = 0;
        while (halvings < MaxHalvings && (smaller >> (halvings + 1)) >= SmallestLevel) halvings++;
        return halvings;
    }

    /// <summary>How many levels this copy carries, and only while the halving is EXACT.
    /// <para>An odd dimension halves to something that is not half of it, so that level covers a slightly different
    /// area than the one above - and the same 0..1 coordinate then means two different places on two levels. Sampled
    /// across levels that shows as the backdrop SLIDING a pixel or two as the blur widens, with the error growing at
    /// every step. Stopping where the halving stops being exact costs a level and removes the slide.</para></summary>
    internal static uint CountLevels(uint width, uint height)
    {
        var levels = 1u;
        while (width > 1 && height > 1 && (width & 1) == 0 && (height & 1) == 0)
        {
            width >>= 1;
            height >>= 1;
            levels++;
        }

        return levels;
    }

    // Each level is DRAWN from the one above it by a thirteen-tap filter (see CaptureBlurEffect). A halving blit was
    // what stood here, and its LINEAR filter is a box: it does not suppress what is above Nyquist before decimating, so
    // a regular pattern behind a pane folded into moire at every level instead of blurring.
    //
    // The levels walk through layouts one at a time - the one just written becomes the SOURCE of the next - which is
    // why every barrier here is per-level and the whole-image form cannot be used.
    private void BuildPyramid(GraphicsDevice gd, CommandBuffer commandBuffer, Texture texture, int width, int height)
    {
        var levels = CountLevels((uint)width, (uint)height);
        if (levels < 2) return;

        _blur ??= new Adamantium.UI.Effects.Generated.CaptureBlurEffect(gd);

        // THE VIEWPORT AND SCISSOR ARE THE FRAME'S, and they are CACHED - the device sends them only when they change.
        // Each level here needs its own, tiny, and leaving the last one behind clipped everything the frame drew after
        // the material away: the pane appeared and the panel beside it did not.
        var viewport = gd.CurrentViewports.Length > 0 ? gd.CurrentViewports[0] : default;
        var scissor = gd.CurrentScissors.Length > 0 ? gd.CurrentScissors[0] : default;

        var w = width;
        var h = height;

        // Level 0 arrives as a transfer destination; it is about to be READ.
        gd.InsertImageMemoryBarrier(commandBuffer, texture,
            AccessFlagBits.TransferWriteBit, AccessFlagBits.ShaderReadBit,
            ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlagBits.TransferBit, PipelineStageFlagBits.FragmentShaderBit,
            0, 1);

        for (var level = 1u; level < levels; level++)
        {
            var nw = Math.Max(1, w / 2);
            var nh = Math.Max(1, h / 2);

            gd.InsertImageMemoryBarrier(commandBuffer, texture,
                AccessFlagBits.None, AccessFlagBits.ColorAttachmentWriteBit,
                ImageLayout.Undefined, ImageLayout.ColorAttachmentOptimal,
                PipelineStageFlagBits.TopOfPipeBit, PipelineStageFlagBits.ColorAttachmentOutputBit,
                level, 1);

            var attachment = new RenderingAttachmentInfo
            {
                ImageView = texture.GetLevelView(level),
                ImageLayout = ImageLayout.ColorAttachmentOptimal,
                LoadOp = AttachmentLoadOp.DontCare,
                StoreOp = AttachmentStoreOp.Store
            };

            var info = new RenderingInfo
            {
                RenderArea = new Rect2D
                {
                    Offset = new Offset2D(),
                    Extent = new Extent2D { Width = (uint)nw, Height = (uint)nh }
                },
                PColorAttachments = new[] { attachment },
                ColorAttachmentCount = 1,
                LayerCount = 1
            };

            commandBuffer.BeginRendering(info);
            gd.SetViewports(new Viewport { Width = nw, Height = nh, MaxDepth = 1.0f });
            gd.SetScissors(info.RenderArea);

            _blur.SourceTexture.SetResource(texture);
            _blur.SourceSampler.SetResource(gd.SamplerStates.LinearClampToEdge);
            _blur.BlurStep.SetValue(new Vector4F(level - 1, nw, nh, 0));
            _blur.CaptureBlurDownPass.Apply();
            gd.Draw(3, 1);

            commandBuffer.EndRendering();

            // What was just drawn becomes the next step's source.
            gd.InsertImageMemoryBarrier(commandBuffer, texture,
                AccessFlagBits.ColorAttachmentWriteBit, AccessFlagBits.ShaderReadBit,
                ImageLayout.ColorAttachmentOptimal, ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlagBits.ColorAttachmentOutputBit, PipelineStageFlagBits.FragmentShaderBit,
                level, 1);

            w = nw;
            h = nh;
        }

        gd.SetViewports(viewport);
        gd.SetScissors(scissor);
    }

    private Adamantium.UI.Effects.Generated.CaptureBlurEffect _blur;


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
                // A PYRAMID, because that is how a wide blur is actually built: not one wide kernel over the full-size
                // copy, but a small one over a smaller image. Each level halves both axes, so level N is a blur of
                // radius 2^N for the price of a single sample - and there is no radius at which it falls apart.
                MipLevels = CountLevels(width, height),
                Samples = MSAALevel.None,
                Format = Format.R8G8B8A8_UNORM,
                InitialLayout = ImageLayout.Undefined,
                DesiredImageLayout = ImageLayout.ShaderReadOnlyOptimal,
                ImageType = ImageType._2d,
                ImageAspect = ImageAspectFlagBits.ColorBit,
                ImageTiling = Vulkan.Core.ImageTiling.Optimal,
                // TransferSrc as well: every level but the last is the SOURCE of the blit that makes the next one.
                // ColorAttachment because a level is also DRAWN into - the filter that fills it is a shader, and a
                // shader writes through an attachment. No descriptor is involved in that direction, which is why this
                // route needs nothing from the heap.
                Usage = ImageUsageFlagBits.SampledBit | ImageUsageFlagBits.TransferDstBit
                        | ImageUsageFlagBits.TransferSrcBit | ImageUsageFlagBits.ColorAttachmentBit,
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
