using Adamantium.Core;
using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public class BitmapHelper
    {
        private const UInt16 fileType = 0x4D42;

        private byte[] PngHeader = { 137, 80, 78, 71, 13, 10, 26, 10 };

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        public struct BitmapFileHeader
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
        public struct BitmapInfoHeader
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

        public enum BitmapCompressionMode : uint
        {
            BI_RGB = 0,
            BI_RLE8 = 1,
            BI_RLE4 = 2,
            BI_BITFIELDS = 3,
            BI_JPEG = 4,
            BI_PNG = 5
        }

        public enum ConvertionFlags
        {
            None,
            RGB555,
            RGB565
        }

        public static class ColorMasks
        {
            //565 RGB masks
            public static class RGB565
            {
                public static ushort RedMask => 0xF800;
                public static ushort GreenMask => 0x7E0;
                public static ushort BlueMask => 0x1F;
            }

            //555 RGB masks
            public static class RGB555
            {
                public static ushort RedMask => 0x7C00;
                public static ushort GreenMask => 0x3E0;
                public static ushort BlueMask => 0x1F;
            }

            public static class R8G8B8A8
            {
                public static uint RedMask => 0x00ff0000;
                public static uint GreenMask => 0x0000ff00;
                public static uint BlueMask => 0x000000ff;
                public static uint AlphaMask => 0xff000000;
                public static uint ColorSpaceType => 0x73524742;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public unsafe struct BitmapColorHeader
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

        public static Image LoadFromBitmapMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            if (!DecodeBitmapHeader(pSource, size, out var description, out var dataOffset, out var compression))
            {
                return null;
            }

            return CreateImageFromBitmap(pSource, dataOffset, description, handle);
        }

        private static unsafe Image CreateImageFromBitmap(IntPtr pSource, int offset, ImageDescription description, GCHandle? handle)
        {
            var originalFormat = description.Format;
            var realFormatSize = FormatHelper.SizeOfInBytes(description.Format);
            description.Format = Format.R8G8B8A8_UNORM;
            var sizeinBytes = FormatHelper.SizeOfInBytes(description.Format);
            var bufferSize = description.Width * description.Height * sizeinBytes;
            byte[] buffer = new byte[bufferSize];
            var rowStride = description.Width * realFormatSize;
            var alignedRowStride = AlignStride(rowStride);
            int rowStrideDiff = alignedRowStride - rowStride;
            var convertionsFlags = ConvertionFlags.None;
            if (realFormatSize == 2)
            {
                convertionsFlags = originalFormat == Format.B5G5R5A1_UNORM_PACK16 ? ConvertionFlags.RGB555 : ConvertionFlags.RGB565;
            }

            using (var stream = new UnmanagedMemoryStream((byte*)pSource, rowStride * description.Height + offset))
            {
                int bufferOffset = 0;
                var streamOffset = 0;
                stream.Seek(offset, SeekOrigin.Begin);
                for (int i = 0; i < bufferSize; i += sizeinBytes)
                {
                    stream.Read(buffer, bufferOffset, realFormatSize);
                    bufferOffset += realFormatSize;
                    streamOffset += realFormatSize;
                    var origin = bufferOffset - realFormatSize;

                    if (realFormatSize == 2)
                    {
                        ushort rg16 = (ushort)((buffer[origin]) | (buffer[origin + 1] << 8));
                        byte r = 0;
                        byte g = 0;
                        byte b = 0;
                        if (convertionsFlags == ConvertionFlags.RGB565)
                        {
                            b = (byte)(rg16 & ColorMasks.RGB565.BlueMask);
                            g = (byte)((rg16 & ColorMasks.RGB565.GreenMask) >> 5);
                            r = (byte)((rg16 & ColorMasks.RGB565.RedMask) >> 11);
                        }
                        else if (convertionsFlags == ConvertionFlags.RGB555)
                        {
                            b = (byte)(rg16 & ColorMasks.RGB555.BlueMask);
                            g = (byte)((rg16 & ColorMasks.RGB555.GreenMask) >> 5);
                            r = (byte)((rg16 & ColorMasks.RGB555.RedMask) >> 10);
                        }

                        r = (byte)Math.Round(255.0d / 31 * r);
                        g = (byte)Math.Round(255.0d / 31 * g);
                        b = (byte)Math.Round(255.0d / 31 * b);

                        buffer[origin] = r;
                        buffer[origin + 1] = g;
                        buffer[origin + 2] = b;
                    }
                    else if (realFormatSize > 2)
                    {
                        Utilities.Swap(ref buffer[origin], ref buffer[origin + 2]);
                    }

                    if (realFormatSize < sizeinBytes)
                    {
                        var bytesDiff = sizeinBytes - realFormatSize;
                        bufferOffset += bytesDiff;
                    }

                    if (rowStride % 4 != 0 && streamOffset % rowStride == 0)
                    {
                        var tmpBuffer = new byte[rowStrideDiff];
                        stream.Read(tmpBuffer, 0, rowStrideDiff);
                    }
                }
            }

            Image image = Image.New(description);
            var bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var ptr = image.PixelBuffer[0].DataPointer;
            Utilities.CopyMemory(ptr, bufferHandle.AddrOfPinnedObject(), buffer.Length);
            bufferHandle.Free();
            image.PixelBuffer[0].FlipBuffer(FlipBufferOptions.FlipVertically);

            return image;
        }

        private static int AlignStride(int stride, int align = 4)
        {
            var newStride = stride;
            while(newStride % align != 0)
            {
                newStride += 1;
            }

            return newStride;
        }

        private static unsafe bool DecodeBitmapHeader(IntPtr headerPtr, int size, out ImageDescription description, out int dataOffset, out BitmapCompressionMode compressionMode)
        {
            dataOffset = 0;
            compressionMode = BitmapCompressionMode.BI_RGB;
            description = new ImageDescription();
            if (headerPtr == IntPtr.Zero)
                throw new ArgumentException("Pointer to Bitmap header cannot be null", nameof(headerPtr));

            if (size < (Utilities.SizeOf<BitmapFileHeader>()))
                return false;

            var fileHeader = Utilities.Read<BitmapFileHeader>(headerPtr);

            if (fileHeader.fileType != fileType)
            {
                return false;
            }

            var infoHeader = Utilities.Read<BitmapInfoHeader>(IntPtr.Add(headerPtr, Marshal.SizeOf<BitmapFileHeader>()));

            // The BMPColorHeader is used only for transparent images
            if (infoHeader.bitCount == 32)
            {
                description.Format = Format.B8G8R8A8_UNORM;
                // Check if the file has bit mask color information
                if (infoHeader.size >= (Marshal.SizeOf<BitmapInfoHeader>() + Marshal.SizeOf<BitmapColorHeader>()))
                {
                    var colorHeader = Utilities.Read<BitmapColorHeader>(IntPtr.Add(headerPtr, Marshal.SizeOf<BitmapInfoHeader>() + Marshal.SizeOf<BitmapFileHeader>()));
                    // Check if the pixel data is stored as BGRA and if the color space type is sRGB
                    CheckColorHeader(colorHeader);
                }
            }
            else if(infoHeader.bitCount == 24)
            {
                description.Format = Format.B8G8R8_UNORM;
            }
            else if (infoHeader.bitCount == 16)
            {
                description.Format = Format.B5G5R5A1_UNORM_PACK16;
            }

            description.MipLevels = 1;
            description.Width = infoHeader.width;
            description.Height = infoHeader.height;
            description.Depth = 1;
            description.ArraySize = 1;
            description.Dimension = TextureDimension.Texture2D;

            dataOffset = (int)fileHeader.offsetData;
            compressionMode = infoHeader.compression;

            return true;
        }

        private static void CheckColorHeader(BitmapColorHeader colorHeader)
        {
            if (ColorMasks.R8G8B8A8.RedMask != colorHeader.redMask ||
                ColorMasks.R8G8B8A8.BlueMask != colorHeader.blueMask ||
                ColorMasks.R8G8B8A8.GreenMask != colorHeader.greenMask ||
                ColorMasks.R8G8B8A8.AlphaMask != colorHeader.alphaMask)
            {
                throw new ArgumentOutOfRangeException("Unexpected color mask format! The program expects the pixel data to be in the BGRA format");
            }
            if (ColorMasks.R8G8B8A8.ColorSpaceType != colorHeader.colorSpaceType)
            {
                throw new ArgumentOutOfRangeException("Unexpected color space type! The program expects sRGB values");
            }
        }

        public static void SaveToBitmapMemory(PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            SaveToBitmapStream(pixelBuffers, count, description, imageStream);
        }

        private static unsafe void SaveToBitmapStream(PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            imageStream.Seek(0, SeekOrigin.Begin);

            var pixelBuffer = pixelBuffers[0];

            uint offsetData = (uint)(Marshal.SizeOf<BitmapFileHeader>() + Marshal.SizeOf<BitmapInfoHeader>() + Marshal.SizeOf<BitmapColorHeader>());
            uint fileSize = offsetData;

            if (pixelBuffer.RowStride % 4 == 0)
            {
                fileSize += (uint)pixelBuffer.BufferStride;
            }
            else
            {
                var width = AlignStride(description.Width);
                var size = width * description.Height * description.Format.SizeOfInBytes();
                fileSize += (uint)size;
            }

            var fileHeader = new BitmapFileHeader();
            fileHeader.fileType = fileType;
            fileHeader.fileSize = fileSize;
            fileHeader.offsetData = offsetData;

            var infoHeader = new BitmapInfoHeader();
            infoHeader.bitCount = (ushort)description.Format.SizeOfInBits();
            infoHeader.width = description.Width;
            infoHeader.height = description.Height;
            infoHeader.planes = 1;
            infoHeader.size = (uint)Marshal.SizeOf<BitmapInfoHeader>();

            var buffer = new byte[fileSize];
            IntPtr fileHeaderMemory = Utilities.AllocateMemory(Marshal.SizeOf<BitmapFileHeader>(), 2);
            Marshal.StructureToPtr(fileHeader, fileHeaderMemory, true);
            Utilities.Read(fileHeaderMemory, buffer, 0, Marshal.SizeOf<BitmapFileHeader>());

            IntPtr infoHeaderMemory = Utilities.AllocateMemory(Marshal.SizeOf<BitmapInfoHeader>());
            Marshal.StructureToPtr(infoHeader, infoHeaderMemory, true);
            Utilities.Read(infoHeaderMemory, buffer, Marshal.SizeOf<BitmapFileHeader>(), Marshal.SizeOf<BitmapInfoHeader>());

            IntPtr colorHeaderMemory = Utilities.AllocateMemory(Marshal.SizeOf<BitmapColorHeader>());
            Marshal.StructureToPtr(infoHeader, colorHeaderMemory, true);
            var bufferOffset = Marshal.SizeOf<BitmapFileHeader>() + Marshal.SizeOf<BitmapInfoHeader>();
            Utilities.Read(colorHeaderMemory, buffer, bufferOffset, Marshal.SizeOf<BitmapInfoHeader>());

            pixelBuffer.FlipBuffer(FlipBufferOptions.FlipVertically);
            if (pixelBuffer.Format.SizeOfInBytes() == 3)
            {
                var colors = pixelBuffer.GetPixels<ColorRGB>();
                for (int i = 0; i < colors.Length; ++i)
                {
                    Utilities.Swap(ref colors[i].R, ref colors[i].B);
                }
                pixelBuffer.SetPixels<ColorRGB>(colors);
            }
            else if (pixelBuffer.Format.SizeOfInBytes() == 4)
            {
                var colors = pixelBuffer.GetPixels<ColorRGBA>();
                for (int i = 0; i < colors.Length; ++i)
                {
                    Utilities.Swap(ref colors[i].R, ref colors[i].B);
                }
                pixelBuffer.SetPixels<ColorRGBA>(colors);
            }


            imageStream.Write(buffer, 0, (int)offsetData);
            bufferOffset = (int)offsetData;
            Utilities.Read(pixelBuffer.DataPointer, buffer, bufferOffset, pixelBuffer.BufferStride);

            if (pixelBuffer.RowStride % 4 == 0)
            {
                imageStream.Write(buffer, bufferOffset, pixelBuffer.BufferStride);
            }
            else
            {
                var alignedWidth = AlignStride(description.Width);
                var alignDiff = alignedWidth - description.Width;
                var rowStride = description.Width * description.Format.SizeOfInBytes();
                for (int i = 0; i < description.Height; ++i)
                {
                    imageStream.Write(buffer, bufferOffset, rowStride);
                    for (int k = 0; k < alignDiff; ++k)
                    {
                        imageStream.WriteByte(0);
                    }
                    bufferOffset += rowStride;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ColorRGB
        {
            public byte R;
            public byte G;
            public byte B;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ColorRGBA
        {
            public byte R;
            public byte G;
            public byte B;
            public byte A;
        }
    };
}
