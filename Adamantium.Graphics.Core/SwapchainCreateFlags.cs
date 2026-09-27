using System;

namespace Adamantium.Graphics.Core
{
    [Flags]
    public enum SwapchainCreateFlags
    {
        SplitInstanceBindRegions = 1,

        Protected = 2,

        MutableFormat = 4,
    }
}
