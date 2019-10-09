using System;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    public class RenderTarget : Texture
    {
        internal RenderTarget(GraphicsDevice device, TextureDescription description) : base(device, description)
        {
            
        }

        public static RenderTarget New(GraphicsDevice graphicsDevice, 
            UInt32 width, 
            UInt32 height, 
            MSAALevel msaa, 
            SurfaceFormat format, 
            ImageLayout desiredLayout = ImageLayout.ColorAttachmentOptimal)
        {
            var usage = ImageUsageFlagBits.TransientAttachmentBit |
                        ImageUsageFlagBits.ColorAttachmentBit;
            
            TextureDescription description = new TextureDescription
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

            return new RenderTarget(graphicsDevice, description);
        }
    }
}