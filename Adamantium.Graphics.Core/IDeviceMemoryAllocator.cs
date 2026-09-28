using System;

namespace Adamantium.Graphics.Core;

/// <summary>The device-memory sub-allocator: one per logical device, owned by <see cref="MainGraphicsDevice"/> and shared by
/// its render devices so they do not each claim a block of the scarce BAR heap.</summary>
public interface IDeviceMemoryAllocator : IDisposable
{
}
