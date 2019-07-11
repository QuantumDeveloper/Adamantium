using System;
using System.Runtime.InteropServices;

namespace Adamantium.Imaging.Bmp
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct BitmapInfoHeader
    {
        //Size of this header (in bytes)
        public UInt32 size;
        //width of bitmap in pixels
        public Int32 width;
        // height of bitmap in pixels
        //(if positive, bottom-up, with origin in lower left corner)
        //(if negative, top-down, with origin in upper left corner)
        public Int32 height;
        // No. of planes for the target device, this is always 1
        public UInt16 planes;
        // No. of bits per pixel
        public UInt16 bitCount;
        // 0 or 3 - uncompressed.
        public BitmapCompressionMode compression;
        // 0 - for uncompressed images
        public UInt32 sizeImage;
        public Int32 xPixelsPerMeter;
        public Int32 yPixelsPerMeter;
        // No. color indexes in the color table. Use 0 for the max number of colors allowed by bit_count
        public UInt32 colorsUsed;
        // No. of colors used for displaying the bitmap. If 0 all colors are required
        public UInt32 colorsImportant;
    };
}