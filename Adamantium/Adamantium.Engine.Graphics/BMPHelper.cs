using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    public class BMPHelper
    {
        private const UInt16 fileType = 0x4D42;

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        public struct BMPFileHeader
        {
            // File type always BM which is 0x4D42
            //[FieldOffset(0)]
            public ushort fileType;
            // Size of the file (in bytes)
            //[FieldOffset(2)]
            public uint fileSize;
            // Reserved, always 0
            //[FieldOffset(6)]
            public ushort reserved1;
            // Reserved, always 0
            //[FieldOffset(8)]
            public ushort reserved2;
            // Start position of pixel data (bytes from the beginning of the file)
            //[FieldOffset(10)]
            public uint offsetData;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct BMPInfoHeader
        {
            //Size of this header (in bytes)
            UInt32 size;
            //width of bitmap in pixels
            Int32 width;
            // width of bitmap in pixels
            //(if positive, bottom-up, with origin in lower left corner)
            //(if negative, top-down, with origin in upper left corner)
            Int32 height;
            // No. of planes for the target device, this is always 1
            UInt16 planes;
            // No. of bits per pixel
            UInt16 bitCount;
            // 0 or 3 - uncompressed. THIS PROGRAM CONSIDERS ONLY UNCOMPRESSED BMP images
            BitmapCompressionMode compression;
            // 0 - for uncompressed images
            UInt32 sizeImage;
            Int32 xPixelsPerMeter;
            Int32 yPixelsPerMeter;
            // No. color indexes in the color table. Use 0 for the max number of colors allowed by bit_count
            UInt32 colorsUsed;
            // No. of colors used for displaying the bitmap. If 0 all colors are required
            UInt32 colorsImportant;
        };

        enum BitmapCompressionMode : uint
        {
            BI_RGB = 0,
            BI_RLE8 = 1,
            BI_RLE4 = 2,
            BI_BITFIELDS = 3,
            BI_JPEG = 4,
            BI_PNG = 5
        }

        UInt32 redMask = 0x00ff0000;
        UInt32 greenMask = 0x0000ff00;
        UInt32 blueMask = 0x000000ff;
        UInt32 alphaMask = 0xff000000;
        UInt32 colorSpaceType = 0x73524742;

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        public struct BMPColorHeader
        {
            // Bit mask for the red channel
            UInt32 redMask;
            // Bit mask for the green channel
            UInt32 greenMask;
            // Bit mask for the blue channel
            UInt32 blueMask;
            // Bit mask for the alpha channel
            UInt32 alphaMask;
            // Default "sRGB" (0x73524742)
            UInt32 colorSpaceType;
            // Unused data for sRGB color space
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            UInt32[] unused;
        }

        public unsafe static Image LoadFromBMPMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            if (!DecodeBMPHeader(pSource, size, out var description))
            {
                return null;
            }

            return null;
        }

        private static unsafe bool DecodeBMPHeader(IntPtr headerPtr, int size, out ImageDescription description)
        {
            description = new ImageDescription();
            if (headerPtr == IntPtr.Zero)
                throw new ArgumentException("Pointer to DDS header cannot be null", "headerPtr");

            if (size < (Utilities.SizeOf<BMPFileHeader>()))
                return false;

            var header = Utilities.Read<BMPFileHeader>(headerPtr);

            if (header.fileType != fileType)
            {
                return false;
            }

            var infoHeader = Utilities.Read<BMPInfoHeader>(IntPtr.Add(headerPtr, Marshal.SizeOf<BMPFileHeader>()));

            description.MipLevels = 1;


            return true;
        }

        public static void SaveToBMPMemory(PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            //SaveToTgaStream(pixelBuffers, count, description, imageStream);
        }
    };
}
