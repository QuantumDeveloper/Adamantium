using System;

namespace Adamantium.Imaging.Dds
{
    /// <summary>
    /// DDS Surface flags.
    /// </summary>
    [Flags]
    internal enum SurfaceFlags
    {
        Texture = 0x00001000, // DDSCAPS_TEXTURE
        Mipmap = 0x00400008,  // DDSCAPS_COMPLEX | DDSCAPS_MIPMAP
        Cubemap = 0x00000008, // DDSCAPS_COMPLEX
    }
}