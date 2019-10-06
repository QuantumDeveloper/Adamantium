using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class DepthStencilBuffer : Texture
    {
        internal DepthStencilBuffer(GraphicsDevice device, TextureDescription description) : base(device, description)
        {
        }

        public static DepthStencilBuffer New(GraphicsDevice graphicsDevice, uint width, uint height, DepthFormat format, MSAALevel msaa, ImageUsageFlagBits usage, ImageAspectFlagBits imageAspect)
        {
            if (imageAspect.HasFlag(ImageAspectFlagBits.DepthBit))
            {
                imageAspect |= ImageAspectFlagBits.StencilBit;
            }

            TextureDescription description = new TextureDescription
            {
                Width = width,
                Height = height,
                Depth = 1,
                Dimension = TextureDimension.Texture2D,
                ArrayLayers = 1,
                Usage = usage,
                Format = (Format)format,
                DesiredImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
                ImageTiling = ImageTiling.Optimal,
                ImageType = ImageType._2d,
                MipLevels = 1,
                SharingMode = SharingMode.Exclusive,
                ImageAspect = imageAspect,
                Samples = msaa
            };

            return (DepthStencilBuffer)New(graphicsDevice, description);
        }
    }
}
