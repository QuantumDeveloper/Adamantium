using AdamantiumVulkan.Core;

namespace Adamantium.Graphics.Core;

/// <summary>
/// The global descriptor heap (VK_EXT_descriptor_heap), owned once per logical device by
/// <see cref="MainGraphicsDevice"/> and shared by all of its render-device wrappers (they share one <c>VkDevice</c>,
/// so a heap-per-window would be wrong). The concrete implementation lives in the Adamantium.Graphics layer;
/// consumers use it through this interface. Only the heap path (<c>EffectPass.UseDescriptorHeap</c>) populates and
/// binds it — in descriptor_buffer mode it allocates nothing.
/// </summary>
public interface IDescriptorHeapManager
{
    PhysicalDeviceDescriptorHeapPropertiesEXT DeviceHeapProperties { get; }

    /// <summary>Reserves an aligned byte offset in the global resource heap.</summary>
    uint AllocateResourceOffset(uint descriptorSize, uint alignment);

    /// <summary>Reserves an aligned byte offset in the global sampler heap.</summary>
    uint AllocateSamplerOffset(uint descriptorSize);

    void WriteTexture(uint offset, ITexture texture, DescriptorType type);

    void WriteBuffer(ulong offset, IBuffer buffer, ulong bufferOffset, ulong bufferSize, DescriptorType type);

    void WriteSampler(uint offset, SamplerState samplerState);

    /// <summary>Binds the shared heap onto the calling render device's command buffer.</summary>
    void BindDescriptorHeaps(IGraphicsDevice device);
}
