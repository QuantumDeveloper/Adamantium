using System;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics;

public static class GraphicsDeviceExtension
{
    public static void CopyImageFromPresenter(this GraphicsDevice graphicsDevice, Texture resolveTexture, Texture destinationTexture)
        {
            ArgumentNullException.ThrowIfNull(resolveTexture);
            ArgumentNullException.ThrowIfNull(destinationTexture);
            
            var commandBuffer = graphicsDevice.CurrentCommandBuffer;
            var copy = new ImageCopy();
            copy.Extent = new Extent3D();
            copy.SrcOffset = new Offset3D();
            copy.DstOffset = new Offset3D();
            copy.Extent.Depth = 1;
            copy.Extent.Width = resolveTexture.Description.Width;
            copy.Extent.Height = resolveTexture.Description.Height;
            copy.SrcSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlagBits.ColorBit, 
                LayerCount = 1
            };
            copy.DstSubresource = new ImageSubresourceLayers
            {
                AspectMask = ImageAspectFlagBits.ColorBit,
                LayerCount = 1
            };

            // resolveTexture.TransitionImageLayout(ImageLayout.TransferSrcOptimal);
            // dstTexture.TransitionImageLayout(ImageLayout.TransferDstOptimal);
            
            var range = new ImageSubresourceRange();
            range.AspectMask = ImageAspectFlagBits.ColorBit;
            range.BaseMipLevel = 0;
            range.LevelCount = (~0U);
            range.BaseArrayLayer = 0;
            range.LayerCount = (~0U);
            graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
                resolveTexture,
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
            
            commandBuffer.CopyImage(resolveTexture, 
                ImageLayout.TransferSrcOptimal, 
                destinationTexture,
                ImageLayout.TransferDstOptimal,
                1,
                copy);
            
            range = new ImageSubresourceRange();
            range.AspectMask = ImageAspectFlagBits.ColorBit;
            range.BaseMipLevel = 0;
            range.LevelCount = (~0U);
            range.BaseArrayLayer = 0;
            range.LayerCount = (~0U);
            graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
                resolveTexture,
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
            
            
            //resolveTexture.TransitionImageLayout(ImageLayout.ColorAttachmentOptimal);
            //dstTexture.TransitionImageLayout(ImageLayout.ShaderReadOnlyOptimal);
        }
}