using System;

namespace Adamantium.Imaging.Dds
{
    /// <summary>
    /// PixelFormat flags.
    /// </summary>
    [Flags]
    internal enum PixelFormatFlags
    {
        FourCC = 0x00000004, // DDPF_FOURCC
        Rgb = 0x00000040, // DDPF_RGB
        Rgba = 0x00000041, // DDPF_RGB | DDPF_ALPHAPIXELS
        Luminance = 0x00020000, // DDPF_LUMINANCE
        LuminanceAlpha = 0x00020001, // DDPF_LUMINANCE | DDPF_ALPHAPIXELS
        Alpha = 0x00000002, // DDPF_ALPHA
        Pal8 = 0x00000020, // DDPF_PALETTEINDEXED8            
    }
}