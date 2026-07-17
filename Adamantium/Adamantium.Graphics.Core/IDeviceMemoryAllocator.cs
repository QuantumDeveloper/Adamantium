using System;

namespace Adamantium.Graphics.Core;

/// <summary>The device-memory sub-allocator, owned once per logical device by <see cref="MainGraphicsDevice"/> and shared
/// by all of its render-device wrappers (they share one <c>VkDevice</c>, so an allocator-per-window would each grab its
/// own big block of the scarce host-visible BAR heap). The concrete implementation lives in the Adamantium.Graphics layer;
/// consumers reach it through <see cref="IGraphicsDevice"/> and cast to the concrete type to sub-allocate. Mirrors
/// <see cref="IDescriptorHeapManager"/>.</summary>
public interface IDeviceMemoryAllocator : IDisposable
{
}
