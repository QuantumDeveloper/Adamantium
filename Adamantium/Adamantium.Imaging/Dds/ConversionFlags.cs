using System;

namespace Adamantium.Imaging.Dds
{
    [Flags]
    internal enum ConversionFlags
    {
        None = 0x0,
        Expand = 0x1, // Conversion requires expanded pixel size
        NoAlpha = 0x2, // Conversion requires setting alpha to known value
        Swizzle = 0x4, // BGR/RGB order swizzling required
        Pal8 = 0x8, // Has an 8-bit palette
        Format888 = 0x10, // Source is an 8:8:8 (24bpp) format
        Format565 = 0x20, // Source is a 5:6:5 (16bpp) format
        Format5551 = 0x40, // Source is a 5:5:5:1 (16bpp) format
        Format4444 = 0x80, // Source is a 4:4:4:4 (16bpp) format
        Format44 = 0x100, // Source is a 4:4 (8bpp) format
        Format332 = 0x200, // Source is a 3:3:2 (8bpp) format
        Format8332 = 0x400, // Source is a 8:3:3:2 (16bpp) format
        FormatA8P8 = 0x800, // Has an 8-bit palette with an alpha channel
        CopyMemory = 0x1000, // The content of the memory passed to the DDS Loader is copied to another internal buffer.
        DX10 = 0x10000, // Has the 'DX10' extension header
    };
}
