using System;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics;

public static class TextureExtensions
{
    public static void TransitionImageLayout(this Texture texture, ImageLayout newLayout)
    {
        var commandBuffer = texture.GraphicsDevice.BeginSingleTimeCommands();

            var imageMemoryBarrier = new ImageMemoryBarrier
            {
                OldLayout = texture.ImageLayout,
                NewLayout = newLayout,
                SrcQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED,
                DstQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED,
                Image = texture,
                SubresourceRange = new ImageSubresourceRange()
            };
            
            imageMemoryBarrier.SubresourceRange.BaseMipLevel = 0;
            imageMemoryBarrier.SubresourceRange.LevelCount = 1;
            imageMemoryBarrier.SubresourceRange.BaseArrayLayer = 0;
            imageMemoryBarrier.SubresourceRange.LayerCount = 1;
            
            if (newLayout == ImageLayout.DepthStencilAttachmentOptimal)
            {
                imageMemoryBarrier.SubresourceRange.AspectMask = ImageAspectFlagBits.DepthBit;

                if (texture.SurfaceFormat.HasStencilFormat())
                {
                    imageMemoryBarrier.SubresourceRange.AspectMask |= ImageAspectFlagBits.StencilBit;
                }
            }
            else
            {
                imageMemoryBarrier.SubresourceRange.AspectMask = ImageAspectFlagBits.ColorBit;
            }
            
            PipelineStageFlagBits sourceStage;
            PipelineStageFlagBits destinationStage;

            switch (texture.ImageLayout)
            {
                case ImageLayout.Undefined:
                    // Image layout is undefined (or does not matter)
                    // Only valid as initial layout
                    // No flags required, listed only for completeness
                    imageMemoryBarrier.SrcAccessMask = 0;
                    sourceStage = PipelineStageFlagBits.TopOfPipeBit;
                    break;

                case ImageLayout.Preinitialized:
                    // Image is preinitialized
                    // Only valid as initial layout for linear images, preserves memory contents
                    // Make sure host writes have been finished
                    imageMemoryBarrier.SrcAccessMask = 0;
                    sourceStage = PipelineStageFlagBits.TopOfPipeBit;
                    break;

                case ImageLayout.ColorAttachmentOptimal:
                    // Image is a color attachment
                    // Make sure any writes to the color buffer have been finished
                    imageMemoryBarrier.SrcAccessMask = AccessFlagBits.ColorAttachmentWriteBit;
                    sourceStage = PipelineStageFlagBits.ColorAttachmentOutputBit;
                    break;

                case ImageLayout.DepthStencilAttachmentOptimal:
                    // Image is a depth/stencil attachment
                    // Make sure any writes to the depth/stencil buffer have been finished
                    imageMemoryBarrier.SrcAccessMask = AccessFlagBits.DepthStencilAttachmentWriteBit;
                    sourceStage = PipelineStageFlagBits.EarlyFragmentTestsBit;
                    break;

                case ImageLayout.TransferSrcOptimal:
                    // Image is a transfer source
                    // Make sure any reads from the image have been finished
                    imageMemoryBarrier.SrcAccessMask = AccessFlagBits.TransferReadBit;
                    sourceStage = PipelineStageFlagBits.TransferBit;
                    break;

                case ImageLayout.TransferDstOptimal:
                    // Image is a transfer destination
                    // Make sure any writes to the image have been finished
                    imageMemoryBarrier.SrcAccessMask = AccessFlagBits.TransferWriteBit;
                    sourceStage = PipelineStageFlagBits.TransferBit;
                    break;

                case ImageLayout.ShaderReadOnlyOptimal:
                    // Image is read by a shader
                    // Make sure any shader reads from the image have been finished
                    imageMemoryBarrier.SrcAccessMask = AccessFlagBits.ShaderReadBit;
                    sourceStage = PipelineStageFlagBits.FragmentShaderBit;
                    break;
                default:
                    throw new ArgumentException(
                        $"Transferring from {texture.ImageLayout} image layout is not supported yet");
            }

            switch (newLayout)
            {
                case ImageLayout.TransferDstOptimal:
                    // Image will be used as a transfer destination
                    // Make sure any writes to the image have been finished
                    imageMemoryBarrier.DstAccessMask = AccessFlagBits.TransferWriteBit;
                    destinationStage = PipelineStageFlagBits.TransferBit;
                    break;

                case ImageLayout.TransferSrcOptimal:
                    // Image will be used as a transfer source
                    // Make sure any reads from the image have been finished
                    imageMemoryBarrier.DstAccessMask = AccessFlagBits.TransferReadBit;
                    destinationStage = PipelineStageFlagBits.TransferBit;
                    break;

                case ImageLayout.ColorAttachmentOptimal:
                    // Image will be used as a color attachment
                    // Make sure any writes to the color buffer have been finished
                    imageMemoryBarrier.DstAccessMask = AccessFlagBits.ColorAttachmentWriteBit;
                    destinationStage = PipelineStageFlagBits.ColorAttachmentOutputBit;
                    break;

                case ImageLayout.DepthStencilAttachmentOptimal:
                    // Image layout will be used as a depth/stencil attachment
                    // Make sure any writes to depth/stencil buffer have been finished
                    imageMemoryBarrier.DstAccessMask |= AccessFlagBits.DepthStencilAttachmentReadBit |
                                                        AccessFlagBits.DepthStencilAttachmentWriteBit;
                    destinationStage = PipelineStageFlagBits.EarlyFragmentTestsBit;
                    break;

                case ImageLayout.ShaderReadOnlyOptimal:
                    // Image will be read in a shader (sampler, input attachment)
                    // Make sure any writes to the image have been finished
                    // if (imageMemoryBarrier.SrcAccessMask == 0)
                    // {
                    //     imageMemoryBarrier.SrcAccessMask = AccessFlagBits.HostWriteBit | AccessFlagBits.TransferWriteBit;
                    // }
                    imageMemoryBarrier.DstAccessMask = AccessFlagBits.ShaderReadBit;
                    destinationStage = PipelineStageFlagBits.FragmentShaderBit;
                    break;
                default:
                    throw new ArgumentException($"Transferring to {newLayout} is not handled yet");
            }

            commandBuffer.PipelineBarrier(
                (uint) sourceStage, 
                (uint) destinationStage, 
                0, 
                0, 
                null, 
                0, 
                null, 
                1,
                imageMemoryBarrier);

            texture.ImageLayout = newLayout;

            texture.GraphicsDevice.EndSingleTimeCommands(commandBuffer);
    }
}