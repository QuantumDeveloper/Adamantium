using System;

namespace Adamantium.Engine.Graphics
{
    [Flags]
    public enum SwapchainCreateFlags
    {
        SplitInstanceBindRegions = 1,

        Protected = 2,

        MutableFormat = 4,
    }
}
