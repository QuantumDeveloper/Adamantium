using System;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using Adamantium.Graphics.Effects;
using AdamantiumVulkan.Core;

namespace Adamantium.Graphics;

public class DescriptorHeapManager : DisposableObject, IDescriptorHeapManager
{
    private readonly IGraphicsDevice graphicsDevice;
    private ulong currentResourceOffset = 0;
    private ulong currentSamplerOffset = 0;
    // The heap is shared by all render devices of one logical device, which may render concurrently (different
    // games run in parallel). Serialize the offset allocators and heap writes so the heap path stays correct.
    private readonly object _heapSync = new object();

    public IBuffer ResourceHeapBuffer { get; private set; }
    public IBuffer SamplerHeapBuffer { get; private set; }
    public ulong UsableResourceSpaceSize { get; private set; }
    public ulong UsableSamplerSpaceSize { get; private set; }

    protected Guid Id { get; }

    public PhysicalDeviceDescriptorHeapPropertiesEXT DeviceHeapProperties => graphicsDevice.Adapter.DeviceHeapProperties;

    public DescriptorHeapManager(IGraphicsDevice graphicsDevice)
    {
        Id = Guid.NewGuid();
        this.graphicsDevice = graphicsDevice;
        InitializeHeaps();
    }

    // Binds the shared heap onto the CALLING render device's command buffer (not the heap's construction device).
    public void BindDescriptorHeaps(IGraphicsDevice device)
    {
        IBuffer resourceBuffer = ResourceHeapBuffer;
        IBuffer samplerBuffer = SamplerHeapBuffer;

        ulong resourceReservedSize = device.Adapter.DeviceHeapProperties.MinResourceHeapReservedRange;
        ulong samplerReservedSize = device.Adapter.DeviceHeapProperties.MinSamplerHeapReservedRange;

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

        device.CurrentCommandBuffer.BindResourceHeapEXT(resourceBindInfo);
        device.CurrentCommandBuffer.BindSamplerHeapEXT(samplerBindInfo);
    }
    
    private void InitializeHeaps()
    {
        // VK_EXT_descriptor_buffer (the default/working path, EffectPass.UseDescriptorHeap == false) never reads
        // this heap: EffectResourceLinker's heap writes are gated off (see ProcessReferenceResources) and the GPU
        // samples via per-pass descriptor buffers. So don't allocate it there at all — it would otherwise burn
        // ~10 MB of the small BAR window (~256 MB on NVIDIA Turing) per render device and scale with window/panel
        // count -> OutOfMemory. Only the heap path needs it (and is then bound via BindDescriptorHeaps).
        if (!EffectPass.UseDescriptorHeap) return;

        var props = graphicsDevice.Adapter.DeviceHeapProperties;

        // IMPORTANT: do not allocate a heap as large as the device maximum. host-visible + device-local memory
        // lives in the small BAR window, so keep it modest even in the heap path.
        ulong resourceHeapSize = Math.Min((ulong)props.MaxResourceHeapSize, 8UL * 1024 * 1024);
        ulong samplerHeapSize = Math.Min((ulong)props.MaxSamplerHeapSize, 2UL * 1024 * 1024);

        resourceHeapSize = Utilities.AlignSize(resourceHeapSize, props.ResourceHeapAlignment);
        samplerHeapSize = Utilities.AlignSize(samplerHeapSize, props.SamplerHeapAlignment);

        UsableResourceSpaceSize = resourceHeapSize - props.MinResourceHeapReservedRange;
        UsableSamplerSpaceSize = samplerHeapSize - props.MinSamplerHeapReservedRange;

        var flags = BufferUsageFlags.DescriptorHeapExt | BufferUsageFlags.ShaderDeviceAddress;
        // Descriptor heaps are written by the CPU and read by the GPU each frame → CPU-to-GPU upload (BAR window).
        var memFlags = BufferMemoryUsage.UploadFromCpuToGpu;

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
        lock (_heapSync)
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
    }
    
    /// <summary>
    /// Allocates an aligned byte offset in the global sampler heap.
    /// </summary>
    public uint AllocateSamplerOffset(uint descriptorSize)
    {
        lock (_heapSync)
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
    }

    // Bindless slot caches: each unique texture/sampler gets ONE stable heap slot (its descriptor written once),
    // instead of one slot per shader parameter that every bind overwrote (which made the last-bound texture/sampler
    // show up on every draw). Keyed by the resource object; render targets keep a stable view, so the slot stays valid.
    private readonly System.Collections.Generic.Dictionary<ITexture, uint> _textureHeapOffsets = new();
    private readonly System.Collections.Generic.Dictionary<SamplerState, uint> _samplerHeapOffsets = new();
    private readonly System.Collections.Generic.Dictionary<IBuffer, uint> _bufferHeapOffsets = new();

    public uint GetOrAllocateBufferOffset(IBuffer buffer, DescriptorType type)
    {
        lock (_heapSync)
        {
            if (_bufferHeapOffsets.TryGetValue(buffer, out var existing)) return existing;

            uint descSize = (uint)DeviceHeapProperties.BufferDescriptorSize;
            uint descAlignment = (uint)(ulong)DeviceHeapProperties.BufferDescriptorAlignment;
            uint offset = AllocateResourceOffset(descSize, descAlignment); // _heapSync is re-entrant
            WriteBuffer(offset, buffer, 0, buffer.TotalSize, type);        // whole-buffer binding
            _bufferHeapOffsets[buffer] = offset;
            return offset;
        }
    }

    public uint GetOrAllocateTextureOffset(ITexture texture, DescriptorType type)
    {
        lock (_heapSync)
        {
            if (_textureHeapOffsets.TryGetValue(texture, out var existing)) return existing;

            uint descSize = (uint)DeviceHeapProperties.ImageDescriptorSize;
            uint descAlignment = (uint)DeviceHeapProperties.ImageDescriptorAlignment;
            uint offset = AllocateResourceOffset(descSize, descAlignment); // _heapSync is re-entrant
            WriteTexture(offset, texture, type);
            _textureHeapOffsets[texture] = offset;
            return offset;
        }
    }

    public uint GetOrAllocateSamplerOffset(SamplerState samplerState)
    {
        lock (_heapSync)
        {
            if (_samplerHeapOffsets.TryGetValue(samplerState, out var existing)) return existing;

            uint descSize = (uint)DeviceHeapProperties.SamplerDescriptorSize;
            uint offset = AllocateSamplerOffset(descSize);
            WriteSampler(offset, samplerState);
            _samplerHeapOffsets[samplerState] = offset;
            return offset;
        }
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