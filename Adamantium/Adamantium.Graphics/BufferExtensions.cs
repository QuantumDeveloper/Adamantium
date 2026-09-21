using Adamantium.Vulkan.Core;

namespace Adamantium.Graphics;

public static class BufferExtensions
{
    public static void CopyFromTexture(this Adamantium.Graphics.Buffer buffer, Texture texture)
    {
        var commandBuffer = buffer.GraphicsDevice.BeginSingleTimeCommand();

        var region = new BufferImageCopy();
        region.BufferOffset = 0;
        region.BufferRowLength = 0;
        region.BufferImageHeight = 0;
        region.ImageSubresource = new ImageSubresourceLayers();
        region.ImageSubresource.AspectMask = ImageAspectFlagBits.ColorBit;
        region.ImageSubresource.MipLevel = 0;
        region.ImageSubresource.BaseArrayLayer = 0;
        region.ImageSubresource.LayerCount = 1;
        region.ImageOffset = new Offset3D() { X = 0, Y = 0, Z = 0};
        region.ImageExtent = new Extent3D() {Width = texture.Description.Width, Height = texture.Description.Height, Depth = texture.Description.Depth}; 
            
        commandBuffer.CopyImageToBuffer(texture, ImageLayout.TransferSrcOptimal, buffer, 1, region);
        buffer.GraphicsDevice.EndSingleTimeCommand(commandBuffer);
    }

    public static void CopyToTexture(this Adamantium.Graphics.Buffer buffer, Texture texture)
    {
        var commandBuffer = buffer.GraphicsDevice.BeginSingleTimeCommand();

        BufferImageCopy region = new BufferImageCopy();
        region.BufferOffset = 0;
        region.BufferRowLength = 0;
        region.BufferImageHeight = 0;
        region.ImageSubresource = new ImageSubresourceLayers();
        region.ImageSubresource.AspectMask = ImageAspectFlagBits.ColorBit;
        region.ImageSubresource.MipLevel = 0;
        region.ImageSubresource.BaseArrayLayer = 0;
        region.ImageSubresource.LayerCount = 1;
        region.ImageOffset = new Offset3D() { X = 0, Y = 0, Z = 0};
        region.ImageExtent = new Extent3D() {Width = texture.Description.Width, Height = texture.Description.Height, Depth = texture.Description.Depth}; 
            
        commandBuffer.CopyBufferToImage(buffer, texture, ImageLayout.TransferDstOptimal, 1, region);
        buffer.GraphicsDevice.EndSingleTimeCommand(commandBuffer);
    }

    public static void CrossBoundCopy(this Adamantium.Graphics.Buffer source, Adamantium.Graphics.Buffer destination)
    {
        unsafe
        {
            var sourceData = source.GraphicsDevice.MapMemory(source, 0, (ulong)source.TotalSize, 0);
            var destinationData = destination.GraphicsDevice.MapMemory(destination, 0, (ulong)destination.TotalSize, 0);
            System.Buffer.MemoryCopy((void*)sourceData, (void*)destinationData, destination.TotalSize, source.TotalSize);
            source.GraphicsDevice.UnmapMemory(source);
            destination.GraphicsDevice.UnmapMemory(destination);
        }
    }
}