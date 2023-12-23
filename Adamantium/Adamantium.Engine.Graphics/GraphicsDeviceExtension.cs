using System;
using System.Diagnostics;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics;

public static class GraphicsDeviceExtension
{
    public static void CopyImageFromPresenter(this GraphicsDevice graphicsDevice, Texture sourceTexture, Texture destinationTexture)
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
            imageCopy.Extent.Width = sourceTexture.Description.Width;
            imageCopy.Extent.Height = sourceTexture.Description.Height;
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
            
            var range = new ImageSubresourceRange();
            range.AspectMask = ImageAspectFlagBits.ColorBit;
            range.BaseMipLevel = 0;
            range.LevelCount = (~0U);
            range.BaseArrayLayer = 0;
            range.LayerCount = (~0U);
            graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
                sourceTexture,
                AccessFlagBits.ColorAttachmentWriteBit,
                AccessFlagBits.TransferReadBit,
                ImageLayout.ColorAttachmentOptimal,
                ImageLayout.TransferSrcOptimal,
                PipelineStageFlagBits.ColorAttachmentOutputBit,
                PipelineStageFlagBits.TransferBit,
                range);
            
            range = new ImageSubresourceRange();
            range.AspectMask = ImageAspectFlagBits.ColorBit;
            range.BaseMipLevel = 0;
            range.LevelCount = (~0U);
            range.BaseArrayLayer = 0;
            range.LayerCount = (~0U);
            graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
                destinationTexture,
                AccessFlagBits.ShaderReadBit,
                AccessFlagBits.TransferWriteBit,
                ImageLayout.ShaderReadOnlyOptimal,
                ImageLayout.TransferDstOptimal,
                PipelineStageFlagBits.FragmentShaderBit,
                PipelineStageFlagBits.TransferBit,
                range);
            
            commandBuffer.CopyImage(sourceTexture, 
                ImageLayout.TransferSrcOptimal, 
                destinationTexture,
                ImageLayout.TransferDstOptimal,
                1,
                imageCopy);
            
            range = new ImageSubresourceRange();
            range.AspectMask = ImageAspectFlagBits.ColorBit;
            range.BaseMipLevel = 0;
            range.LevelCount = (~0U);
            range.BaseArrayLayer = 0;
            range.LayerCount = (~0U);
            graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
                sourceTexture,
                AccessFlagBits.TransferReadBit,
                AccessFlagBits.ColorAttachmentWriteBit,
                ImageLayout.TransferSrcOptimal,
                ImageLayout.ColorAttachmentOptimal,
                PipelineStageFlagBits.TransferBit,
                PipelineStageFlagBits.ColorAttachmentOutputBit,
                range);
            
            range = new ImageSubresourceRange();
            range.AspectMask = ImageAspectFlagBits.ColorBit;
            range.BaseMipLevel = 0;
            range.LevelCount = (~0U);
            range.BaseArrayLayer = 0;
            range.LayerCount = (~0U);
            graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
                destinationTexture,
                AccessFlagBits.TransferWriteBit,
                AccessFlagBits.ShaderReadBit,
                ImageLayout.TransferDstOptimal,
                ImageLayout.ShaderReadOnlyOptimal,
                PipelineStageFlagBits.TransferBit,
                PipelineStageFlagBits.FragmentShaderBit,
                range);
            
            
            sourceTexture.TransitionImageLayout(ImageLayout.ColorAttachmentOptimal);
            destinationTexture.TransitionImageLayout(ImageLayout.ShaderReadOnlyOptimal);
        }
}