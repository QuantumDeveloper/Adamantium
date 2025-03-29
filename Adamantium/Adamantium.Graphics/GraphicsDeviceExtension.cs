using System.Diagnostics;
using Adamantium.Graphics.Core;
using AdamantiumVulkan.Core;

namespace Adamantium.Graphics;

public static class GraphicsDeviceExtension
{
    public static void CopyImage(this IGraphicsDevice graphicsDevice, ITexture sourceTexture, ITexture destinationTexture)
        {
            if (sourceTexture == null)
            {
                Debug.WriteLine("Resolve Texture is null");
                return;
            }
            
            if (destinationTexture == null)
            {
                Debug.WriteLine("Destination Texture is null");
                return;
            }
            
            var commandBuffer = graphicsDevice.CurrentCommandBuffer;
            var imageCopy = new ImageCopy();
            imageCopy.Extent = new Extent3D();
            imageCopy.SrcOffset = new Offset3D();
            imageCopy.DstOffset = new Offset3D();
            imageCopy.Extent.Depth = 1;
            imageCopy.Extent.Width = sourceTexture.Width;
            imageCopy.Extent.Height = sourceTexture.Height;
            imageCopy.SrcSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlagBits.ColorBit, 
                LayerCount = 1
            };
            imageCopy.DstSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlagBits.ColorBit,
                LayerCount = 1
            };

            sourceTexture.TransitionImageLayout(ImageLayout.TransferSrcOptimal);
            destinationTexture.TransitionImageLayout(ImageLayout.TransferDstOptimal);
            
            graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
                sourceTexture,
                AccessFlagBits.ColorAttachmentWriteBit,
                AccessFlagBits.TransferReadBit,
                ImageLayout.ColorAttachmentOptimal,
                ImageLayout.TransferSrcOptimal,
                PipelineStageFlagBits.ColorAttachmentOutputBit,
                PipelineStageFlagBits.TransferBit);
            
            graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
                destinationTexture,
                AccessFlagBits.ShaderReadBit,
                AccessFlagBits.TransferWriteBit,
                ImageLayout.ShaderReadOnlyOptimal,
                ImageLayout.TransferDstOptimal,
                PipelineStageFlagBits.FragmentShaderBit,
                PipelineStageFlagBits.TransferBit);
            
            commandBuffer.CopyImage(sourceTexture.GetImage(), 
                ImageLayout.TransferSrcOptimal, 
                destinationTexture.GetImage(),
                ImageLayout.TransferDstOptimal,
                1,
                imageCopy);
            
            graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
                sourceTexture,
                AccessFlagBits.TransferReadBit,
                AccessFlagBits.ColorAttachmentWriteBit,
                ImageLayout.TransferSrcOptimal,
                ImageLayout.ColorAttachmentOptimal,
                PipelineStageFlagBits.TransferBit,
                PipelineStageFlagBits.ColorAttachmentOutputBit);
            
            graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
                destinationTexture,
                AccessFlagBits.TransferWriteBit,
                AccessFlagBits.ShaderReadBit,
                ImageLayout.TransferDstOptimal,
                ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlagBits.TransferBit,
                PipelineStageFlagBits.FragmentShaderBit);
            
            sourceTexture.TransitionImageLayout(ImageLayout.ColorAttachmentOptimal);
            destinationTexture.TransitionImageLayout(ImageLayout.ShaderReadOnlyOptimal);
        }
}