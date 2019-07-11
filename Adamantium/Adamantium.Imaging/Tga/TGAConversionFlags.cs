using System;

namespace Adamantium.Imaging.Tga
{
    [Flags]
    internal enum TGAConversionFlags
    {
        None = 0x0,
        Expand = 0x1,        //Conversion requires expanded pixel size
        InvertX = 0x2,       //If set, scanlines are right to left
        InvertY = 0x4,       //If set, scanlinew are top to bottom
        RLE = 0x8,           //Source data is RLE compressed
        Swizzle = 0x10000,   //Swizzle BGR<->RGB data
        Format888 = 0x20000  // 24bpp format
    }
}