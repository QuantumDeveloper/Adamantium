using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class DepthStencilBuffer : Texture
    {
        internal DepthStencilBuffer(GraphicsDevice device, TextureDescription description) : base(device, description)
        {
        }

        public static DepthStencilBuffer New(GraphicsDevice graphicsDevice, 
            uint width, 
            uint height, 
            DepthFormat format, 
            MSAALevel msaa,
            ImageAspectFlagBits imageAspect = ImageAspectFlagBits.DepthBit)
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
                Usage = ImageUsageFlagBits.DepthStencilAttachmentBit,
                Format = (Format)format,
                DesiredImageLayout = ImageLayout.DepthStencilAttachmentOptimal,
                ImageTiling = ImageTiling.Optimal,
                ImageType = ImageType._2d,
                MipLevels = 1,
                SharingMode = SharingMode.Exclusive,
                ImageAspect = imageAspect,
                Samples = msaa
            };

            return new DepthStencilBuffer(graphicsDevice, description);
        }
    }
}
