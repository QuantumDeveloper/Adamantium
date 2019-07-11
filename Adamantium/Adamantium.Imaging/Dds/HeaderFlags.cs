using System;

namespace Adamantium.Imaging.Dds
{
    /// <summary>
    /// DDS Header flags.
    /// </summary>
    [Flags]
    internal enum HeaderFlags
    {
        Texture = 0x00001007, // DDSD_CAPS | DDSD_HEIGHT | DDSD_WIDTH | DDSD_PIXELFORMAT 
        Mipmap = 0x00020000, // DDSD_MIPMAPCOUNT
        Volume = 0x00800000, // DDSD_DEPTH
        Pitch = 0x00000008, // DDSD_PITCH
        LinearSize = 0x00080000, // DDSD_LINEARSIZE
        Height = 0x00000002, // DDSD_HEIGHT
        Width = 0x00000004, // DDSD_WIDTH
    };
}