using System;

namespace Adamantium.Graphics.Core
{
    [Flags]
    public enum MemoryPropertyFlags
    {
        DeviceLocal = 1,

        HostVisible = 2,

        HostCoherent = 4,

        HostCached = 8,

        LazilyAllocated = 16,

        Protected = 32,
    }
}
