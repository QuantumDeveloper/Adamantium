using System;

namespace Adamantium.Engine.Graphics
{
    [Flags]
    public enum AlphaMode
    {
        Opaque = 1,
        PreMultiplied = 2,
        PostMultiplied = 4,
        Inherit = 8
    }
}