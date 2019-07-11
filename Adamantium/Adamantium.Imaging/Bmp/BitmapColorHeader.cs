using System;
using System.Runtime.InteropServices;

namespace Adamantium.Imaging.Bmp
{
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct BitmapColorHeader
    {
        // Bit mask for the red channel
        public UInt32 redMask;
        // Bit mask for the green channel
        public UInt32 greenMask;
        // Bit mask for the blue channel
        public UInt32 blueMask;
        // Bit mask for the alpha channel
        public UInt32 alphaMask;
        // Default "sRGB" (0x73524742)
        public UInt32 colorSpaceType;
        // Unused data for sRGB color space
        public fixed UInt32 unused[16];
    }
}