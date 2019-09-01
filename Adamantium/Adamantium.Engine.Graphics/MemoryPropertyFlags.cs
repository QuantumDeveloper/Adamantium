using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics
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
