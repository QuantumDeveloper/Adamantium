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

    /// <summary>Returns a STABLE heap offset for a texture (bindless): on first use it reserves a slot and writes
    /// the descriptor, then caches and returns that offset. Each unique texture gets its own slot, so binding
    /// texture A then B no longer overwrites a single per-parameter slot (the "last texture wins everywhere" bug).</summary>
    uint GetOrAllocateTextureOffset(ITexture texture, DescriptorType type);

    /// <summary>As <see cref="GetOrAllocateTextureOffset"/>, but for samplers (their own stable slot per sampler).</summary>
    uint GetOrAllocateSamplerOffset(SamplerState samplerState);

    /// <summary>As <see cref="GetOrAllocateTextureOffset"/>, but for a buffer resource (UAV / storage buffer).
    /// Cached by buffer object and assumes whole-buffer binding (offset 0, full size); extend the key to
    /// (buffer, offset, size) if sub-range bindings are ever used.</summary>
    uint GetOrAllocateBufferOffset(IBuffer buffer, DescriptorType type);

    /// <summary>Binds the shared heap onto the calling render device's command buffer.</summary>
    void BindDescriptorHeaps(IGraphicsDevice device);
}
