using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Identifies the type of resource being used.
    /// </summary>
    public enum ResourceDimension
    {
        Unknown = 0,
        Buffer = 1,
        Texture1D = 2,
        Texture2D = 3,
        Texture3D = 4
    }
}
