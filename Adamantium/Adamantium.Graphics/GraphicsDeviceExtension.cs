using System.Diagnostics;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Extensions;
using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics;

public static class GraphicsDeviceExtension
{
    /// <summary>
    /// Records (into the device's CURRENT command buffer, no separate submit) a copy of a just-resolved render
    /// target into a shared surface that is sampled directly by the consumer. Producer calls this after EndDraw and
    /// before Submit; the Submit then signals Produce. The resolve <paramref name="source"/> is transitioned
    /// ColorAttachment↔TransferSrc and the shared <paramref name="destination"/> ShaderReadOnly↔TransferDst, so the
    /// destination is left in ShaderReadOnly ready for the consumer's sample.
    /// </summary>
    public static void RecordSharedSurfaceCopy(this IGraphicsDevice graphicsDevice, ITexture source, ITexture destination)
    {
        if (source == null || destination == null) return;
        var commandBuffer = graphicsDevice.CurrentCommandBuffer;

        graphicsDevice.InsertImageMemoryBarrier(commandBuffer, source,
            AccessFlagBits.ColorAttachmentWriteBit, AccessFlagBits.TransferReadBit,
            ImageLayout.ColorAttachmentOptimal, ImageLayout.TransferSrcOptimal,
            PipelineStageFlagBits.ColorAttachmentOutputBit, PipelineStageFlagBits.TransferBit);

        graphicsDevice.InsertImageMemoryBarrier(commandBuffer, destination,
            AccessFlagBits.ShaderReadBit, AccessFlagBits.TransferWriteBit,
            ImageLayout.ShaderReadOnlyOptimal, ImageLayout.TransferDstOptimal,
            PipelineStageFlagBits.FragmentShaderBit, PipelineStageFlagBits.TransferBit);

        var imageCopy = new ImageCopy
        {
            Extent = new Extent3D { Width = source.Width, Height = source.Height, Depth = 1 },
            SrcOffset = new Offset3D(),
            DstOffset = new Offset3D(),
            SrcSubresource = new ImageSubresourceLayers { AspectMask = ImageAspectFlagBits.ColorBit, LayerCount = 1 },
            DstSubresource = new ImageSubresourceLayers { AspectMask = ImageAspectFlagBits.ColorBit, LayerCount = 1 }
        };
        commandBuffer.CopyImage(source.GetImage(), ImageLayout.TransferSrcOptimal,
            destination.GetImage(), ImageLayout.TransferDstOptimal, 1, imageCopy);

        graphicsDevice.InsertImageMemoryBarrier(commandBuffer, source,
            AccessFlagBits.TransferReadBit, AccessFlagBits.ColorAttachmentWriteBit,
            ImageLayout.TransferSrcOptimal, ImageLayout.ColorAttachmentOptimal,
            PipelineStageFlagBits.TransferBit, PipelineStageFlagBits.ColorAttachmentOutputBit);

        graphicsDevice.InsertImageMemoryBarrier(commandBuffer, destination,
            AccessFlagBits.TransferWriteBit, AccessFlagBits.ShaderReadBit,
            ImageLayout.TransferDstOptimal, ImageLayout.ShaderReadOnlyOptimal,
            PipelineStageFlagBits.TransferBit, PipelineStageFlagBits.FragmentShaderBit);
    }

    public static void CopyImage(this IGraphicsDevice graphicsDevice, ITexture sourceTexture,
        ITexture destinationTexture)
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

        var commandBuffer = graphicsDevice.BeginSingleTimeCommand();
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
        
        graphicsDevice.EndSingleTimeCommand(commandBuffer);
    }
}