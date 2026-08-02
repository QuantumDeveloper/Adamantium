using System;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics.Core.Extensions;

public static class TextureExtensions
{
    public static void TransitionImageLayout(this ITexture texture, ImageLayout newLayout)
    {
        var commandBuffer = texture.GraphicsDevice.BeginSingleTimeCommand();

        var imageMemoryBarrier = new ImageMemoryBarrier
        {
            OldLayout = texture.ImageLayout,
            NewLayout = newLayout,
            SrcQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED,
            DstQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED,
            Image = texture.GetImage(),
            SubresourceRange = new ImageSubresourceRange()
        };

        imageMemoryBarrier.SubresourceRange.BaseMipLevel = 0;
        imageMemoryBarrier.SubresourceRange.LevelCount = 1;
        imageMemoryBarrier.SubresourceRange.BaseArrayLayer = 0;
        // ALL layers, not just the first. A layout transition applies to the subresources the barrier names, so with a
        // count of 1 an array texture had only layer 0 moved: the rest stayed as created, and both the upload into them
        // and the shader's read of them were reads of an image in the wrong layout. Validation says it plainly
        // ("arrayLayer = 1 ... expects TRANSFER_DST_OPTIMAL, current layout is PREINITIALIZED"), and the draw that
        // followed took the whole renderer down with it.
        imageMemoryBarrier.SubresourceRange.LayerCount = Constants.VK_REMAINING_ARRAY_LAYERS;

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
            case ImageLayout.PresentSrcKhr:
                imageMemoryBarrier.SrcAccessMask = AccessFlagBits.None;
                sourceStage = PipelineStageFlagBits.BottomOfPipeBit;
                break;
            case ImageLayout.General:
                // Host image copy read the image (Texture.Save); the access happened on the host.
                imageMemoryBarrier.SrcAccessMask = AccessFlagBits.HostReadBit;
                sourceStage = PipelineStageFlagBits.HostBit;
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
                imageMemoryBarrier.DstAccessMask = AccessFlagBits.ShaderReadBit;
                destinationStage = PipelineStageFlagBits.FragmentShaderBit;
                break;
            case ImageLayout.PresentSrcKhr:
                imageMemoryBarrier.DstAccessMask = AccessFlagBits.None;
                destinationStage = PipelineStageFlagBits.BottomOfPipeBit;
                break;
            case ImageLayout.General:
                // Will be read by host image copy (Texture.Save) right after this transition.
                imageMemoryBarrier.DstAccessMask = AccessFlagBits.HostReadBit;
                destinationStage = PipelineStageFlagBits.HostBit;
                break;
            default:
                throw new ArgumentException($"Transferring to {newLayout} is not handled yet");
        }

        commandBuffer.PipelineBarrier(
            sourceStage,
            destinationStage,
            0,
            0,
            null,
            0,
            null,
            1,
            imageMemoryBarrier);

        texture.ImageLayout = newLayout;

        texture.GraphicsDevice.EndSingleTimeCommand(commandBuffer);
    }

    public static void BlitImage(this IGraphicsDevice graphicsDevice, CommandBuffer commandBuffer, ITexture srcTexture, ITexture dstTexture)
    {
        var blit = new ImageBlit();
        blit.SrcSubresource = new ImageSubresourceLayers();
        blit.SrcSubresource.AspectMask     = ImageAspectFlagBits.ColorBit;
        blit.SrcSubresource.BaseArrayLayer = 0;
        blit.SrcSubresource.LayerCount     = 1;
        blit.SrcSubresource.MipLevel       = 0;
        var srcOffsets = new Offset3D[2];
        srcOffsets[0] = new Offset3D() {X = 0, Y = 0, Z = 0};
        srcOffsets[1] = new Offset3D() {X = (int)srcTexture.Width, Y = (int)srcTexture.Height, Z = 1};
        blit.SrcOffsets = srcOffsets; 

        // Copy color from source to destination of screen size
        blit.DstSubresource = new ImageSubresourceLayers();
        blit.DstSubresource.AspectMask     = ImageAspectFlagBits.ColorBit;
        blit.DstSubresource.BaseArrayLayer = 0;
        blit.DstSubresource.LayerCount     = 1;
        blit.DstSubresource.MipLevel       = 0;
        var dstOffsets = new Offset3D[2];
        dstOffsets[0] = new Offset3D() {X = 0, Y = 0, Z = 0};
        dstOffsets[1] = new Offset3D() {X = (int)dstTexture.Width, Y = (int)dstTexture.Height, Z = 1};
        blit.DstOffsets = dstOffsets;

        graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
            srcTexture,
            AccessFlagBits.ColorAttachmentWriteBit,
            AccessFlagBits.TransferReadBit,
            //ImageLayout.ColorAttachmentOptimal,
            srcTexture.ImageLayout,
            ImageLayout.TransferSrcOptimal,
            PipelineStageFlagBits.ColorAttachmentOutputBit,
            PipelineStageFlagBits.TransferBit
        );
        
        // destination (swapchain) texture
        graphicsDevice.InsertImageMemoryBarrier(commandBuffer,
            dstTexture,
            0,
            AccessFlagBits.TransferWriteBit,
            ImageLayout.Undefined,
            ImageLayout.TransferDstOptimal,
            PipelineStageFlagBits.TopOfPipeBit,
            PipelineStageFlagBits.TransferBit
        );

        commandBuffer.BlitImage(
            srcTexture.GetImage(), 
            srcTexture.ImageLayout, 
            dstTexture.GetImage(),
            dstTexture.ImageLayout, 
            1, 
            blit, 
            Filter.Linear);

    }
}