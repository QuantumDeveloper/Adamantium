using System;
using Adamantium.Graphics.Core;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.Graphics
{
    public class RenderTarget : Texture, IRenderTarget
    {
        internal RenderTarget(IGraphicsDevice device, TextureDescription description, string name = "") : base(device, description, name)
        {
            if (description.Samples != MSAALevel.None)
            {
                var clonedDesc = description;
                clonedDesc.Samples = MSAALevel.None;
                ResolveTexture = ToDispose(Texture.New(device, clonedDesc, $"{name}:Resolve"));
            }
            else
            {
                ResolveTexture = this;
            }
        }

        public static RenderTarget New(IGraphicsDevice graphicsDevice, 
            UInt32 width, 
            UInt32 height, 
            MSAALevel msaa, 
            SurfaceFormat format, 
            ImageUsageFlagBits usage = ImageUsageFlagBits.TransferSrcBit,
            ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal,
            string name = "")
        {
            usage |= ImageUsageFlagBits.ColorAttachmentBit | ImageUsageFlagBits.SampledBit;

            // NOTE: do NOT add HostTransferBit here. On GPUs without Resizable BAR (e.g. the dev Quadro RTX 4000: a
            // 214 MB DEVICE_LOCAL|HOST_VISIBLE heap), that flag forces the image into a host-visible memory type, and
            // FindMemoryIndex(DeviceLocal) then lands it in that tiny 214 MB BAR window instead of the 8 GB VRAM heap
            // -> a single 4K MSAA target exhausts it (ErrorOutOfDeviceMemory). Plain render targets must live in pure
            // DEVICE_LOCAL VRAM. Texture.Save falls back to a staging read-back when HostTransferBit is absent.

            var description = new TextureDescription
            {
                Width = width,
                Height = height,
                Depth = 1,
                Dimension = TextureDimension.Texture2D,
                ArrayLayers = 1,
                Usage = usage,
                Format = format,
                DesiredImageLayout = desiredLayout,
                ImageTiling = ImageTiling.Optimal,
                ImageType = ImageType._2d,
                MipLevels = 1,
                SharingMode = SharingMode.Exclusive,
                ImageAspect = ImageAspectFlagBits.ColorBit,
                Samples = msaa
            };

            return new RenderTarget(graphicsDevice, description, name);
        }

        public ITexture ResolveTexture { get; }
    }
}