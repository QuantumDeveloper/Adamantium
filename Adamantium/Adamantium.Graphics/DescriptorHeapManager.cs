using System;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Effects;
using AdamantiumVulkan.Core;

namespace Adamantium.Graphics;

public class DescriptorHeapManager : DisposableObject
{
    private readonly IGraphicsDevice graphicsDevice;
    private ulong currentResourceOffset = 0;
    private ulong currentSamplerOffset = 0;
    private EffectPass.DynamicBufferPool[] _dynamicBufferPool;

    public Buffer ResourceHeapBuffer { get; private set; }
    public Buffer SamplerHeapBuffer { get; private set; }
    public ulong UsableResourceSpaceSize { get; private set; }
    public ulong UsableSamplerSpaceSize { get; private set; }
    
    protected Guid Id { get; }
    
    public PhysicalDeviceDescriptorHeapPropertiesEXT DeviceHeapProperties => graphicsDevice.Adapter.DeviceHeapProperties;
    
    public EffectPass.DynamicBufferPool CurrentBufferPool => _dynamicBufferPool[graphicsDevice.CurrentFrame];
    
    public DescriptorHeapManager(IGraphicsDevice graphicsDevice)
    {
        Id = Guid.NewGuid();
        this.graphicsDevice = graphicsDevice;
        InitializeHeaps();
        _dynamicBufferPool = new EffectPass.DynamicBufferPool[graphicsDevice.MaxFramesInFlight];
        for (int i = 0; i < graphicsDevice.MaxFramesInFlight; i++)
        {
            _dynamicBufferPool[i] = new EffectPass.DynamicBufferPool(this, graphicsDevice);
        }
    }
    
    public void BindDescriptorHeaps()
    {
        IBuffer resourceBuffer = ResourceHeapBuffer; 
        IBuffer samplerBuffer = SamplerHeapBuffer;
        
        ulong resourceReservedSize = graphicsDevice.Adapter.DeviceHeapProperties.MinResourceHeapReservedRange;
        ulong samplerReservedSize = graphicsDevice.Adapter.DeviceHeapProperties.MinSamplerHeapReservedRange;

        var resourceAddressRange = new DeviceAddressRangeKHR();
        resourceAddressRange.Address = resourceBuffer.GetDeviceAddress();
        resourceAddressRange.Size = resourceBuffer.TotalSize;

        var resourceBindInfo = new BindHeapInfoEXT();
        resourceBindInfo.HeapRange = resourceAddressRange;

        resourceBindInfo.ReservedRangeOffset = resourceBuffer.TotalSize - resourceReservedSize;
        resourceBindInfo.ReservedRangeSize = resourceReservedSize;

        var samplerAddressRange = new DeviceAddressRangeKHR();
        samplerAddressRange.Address = samplerBuffer.GetDeviceAddress();
        samplerAddressRange.Size = samplerBuffer.TotalSize;

        var samplerBindInfo = new BindHeapInfoEXT();
        samplerBindInfo.HeapRange = samplerAddressRange;

        samplerBindInfo.ReservedRangeOffset = samplerBuffer.TotalSize - samplerReservedSize;
        samplerBindInfo.ReservedRangeSize = samplerReservedSize;

        graphicsDevice.CurrentCommandBuffer.BindResourceHeapEXT(resourceBindInfo);
        graphicsDevice.CurrentCommandBuffer.BindSamplerHeapEXT(samplerBindInfo);
    }
    
    private void InitializeHeaps()
    {
        var props = graphicsDevice.Adapter.DeviceHeapProperties;

        // IMPORTANT: do not allocate a heap as large as the device maximum. host-visible + device-local
        // memory lives in the BAR window (often only 256 MB), so an allocation of hundreds of MB/GB is
        // either not resident (the GPU reads garbage from descriptors -> black screen) or prevents tools
        // from making a replay copy of the heap. Use a sane bounded size, as the reference does.
        ulong resourceHeapSize = Math.Min((ulong)props.MaxResourceHeapSize, 32UL * 1024 * 1024);
        ulong samplerHeapSize = Math.Min((ulong)props.MaxSamplerHeapSize, 2UL * 1024 * 1024);

        resourceHeapSize = Utilities.AlignSize(resourceHeapSize, props.ResourceHeapAlignment);
        samplerHeapSize = Utilities.AlignSize(samplerHeapSize, props.SamplerHeapAlignment);

        UsableResourceSpaceSize = resourceHeapSize - props.MinResourceHeapReservedRange;
        UsableSamplerSpaceSize = samplerHeapSize - props.MinSamplerHeapReservedRange;

        var flags = BufferUsageFlags.DescriptorHeapExt | BufferUsageFlags.ShaderDeviceAddress;
        var memFlags = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.DeviceLocal | MemoryPropertyFlags.HostCoherent;

        ResourceHeapBuffer = Buffer.New(graphicsDevice, resourceHeapSize, flags, memFlags);
        SamplerHeapBuffer = Buffer.New(graphicsDevice, samplerHeapSize, flags, memFlags);
    }

    /// <summary>
    /// Allocates an aligned byte offset in the global resource heap.
    /// </summary>
    /// <param name="descriptorSize">Descriptor size.</param>
    /// <param name="alignment">Alignment for the specific descriptor type (BufferDescriptorAlignment or ImageDescriptorAlignment).</param>
    /// <returns>The aligned byte offset to write the descriptor at.</returns>
    public uint AllocateResourceOffset(uint descriptorSize, uint alignment)
    {
        ulong alignedOffset = Utilities.AlignSize(currentResourceOffset, alignment);

        if (alignedOffset + descriptorSize > UsableResourceSpaceSize)
        {
            throw new OutOfMemoryException(
                $"Resource descriptor heap overflow: failed to allocate {descriptorSize} bytes. " +
                $"Current offset: {alignedOffset}, usable heap limit: {UsableResourceSpaceSize} bytes."
            );
        }

        currentResourceOffset = alignedOffset + descriptorSize;
        return (uint)alignedOffset;
    }
    
    /// <summary>
    /// Allocates an aligned byte offset in the global sampler heap.
    /// </summary>
    public uint AllocateSamplerOffset(uint descriptorSize)
    {
        uint alignment = (uint)DeviceHeapProperties.SamplerDescriptorAlignment;
        ulong alignedOffset = Utilities.AlignSize(currentSamplerOffset, alignment);

        if (alignedOffset + descriptorSize > UsableSamplerSpaceSize)
        {
            throw new OutOfMemoryException("Sampler descriptor heap overflow.");
        }

        currentSamplerOffset = alignedOffset + descriptorSize;
        return (uint)alignedOffset;
    }

    public void WriteTexture(uint offset, ITexture texture, DescriptorType type)
    {
        uint descriptorSize = (uint)graphicsDevice.Adapter.DeviceHeapProperties.ImageDescriptorSize;

        var imageInfo = new ImageDescriptorInfoEXT
        {
            PView = texture.Info,
            Layout = texture.ImageLayout
        };

        var resourceInfo = new ResourceDescriptorInfoEXT 
        {
            Type = type
        };
        resourceInfo.Data = new ResourceDescriptorDataEXT();
        resourceInfo.Data.PImage = imageInfo;

        WriteToResourceHeap(offset, descriptorSize, resourceInfo);
    }
    
    public void WriteBuffer(ulong offset, IBuffer buffer, ulong bufferOffset, ulong bufferSize, DescriptorType type)
    {
        uint descriptorSize = (uint)graphicsDevice.Adapter.DeviceHeapProperties.BufferDescriptorSize;

        // Pass the GPU address of this specific object's data start (base + offset within the arena)
        var bufferRange = new DeviceAddressRangeKHR() 
        {
            Address = buffer.GetDeviceAddress() + bufferOffset,
            Size = bufferSize
        };

        var resourceInfo = new ResourceDescriptorInfoEXT
        {
            Type = type
        };
        resourceInfo.Data = new ResourceDescriptorDataEXT();
        resourceInfo.Data.PAddressRange = bufferRange;

        WriteToResourceHeap(offset, descriptorSize, resourceInfo);
    }
    
    public unsafe void WriteSampler(uint offset, SamplerState samplerState)
    {
        uint descriptorSize = (uint)graphicsDevice.Adapter.DeviceHeapProperties.SamplerDescriptorSize;

        var dataPtr = SamplerHeapBuffer.MapMemory();
        void* target = (byte*)dataPtr + offset;
        var range = new HostAddressRangeEXT
        {
            Address = (nuint)target,
            Size = descriptorSize
        };

        graphicsDevice.LogicalDevice.WriteSamplerDescriptorsEXT(1, samplerState.Info, range);
        SamplerHeapBuffer.UnmapMemory();
    }

    private unsafe void WriteToResourceHeap(ulong offset, uint size, ResourceDescriptorInfoEXT info)
    {
        var dataPtr = ResourceHeapBuffer.MapMemory();
        void* target = (byte*)dataPtr + offset;
        var range = new HostAddressRangeEXT
        {
            Address = (nuint)target,
            Size = size
        };

        graphicsDevice.LogicalDevice.WriteResourceDescriptorsEXT(1, info, range);
        ResourceHeapBuffer.UnmapMemory();
    }
}