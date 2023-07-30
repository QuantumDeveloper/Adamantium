using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging.Bmp
{
    public class BmpHelper
    {
        private const UInt16 fileType = 0x4D42;

        public static IRawBitmap LoadFromMemory(IntPtr pSource, long size)
        {
            if (!DecodeBitmapHeader(pSource, size, out var description, out var dataOffset, out var compression))
            {
                throw new ArgumentException("Given file is not a BMP file");
            }

            return CreateImageFromBitmap(pSource, dataOffset, description);
        }
        
        private static bool DecodeBitmapHeader(IntPtr headerPtr, long size, out ImageDescription description, out int dataOffset, out BitmapCompressionMode compressionMode)
        {
            dataOffset = 0;
            compressionMode = BitmapCompressionMode.RGB;
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
                description.Format = Format.B5G6R5_UNORM_PACK16;
            }

            description.MipLevels = 1;
            description.Width = (uint)infoHeader.width;
            description.Height = (uint)infoHeader.height;
            description.Depth = 1;
            description.ArraySize = 1;
            description.Dimension = TextureDimension.Texture2D;

            dataOffset = (int)fileHeader.offsetData;
            compressionMode = infoHeader.compression;

            return true;
        }

        private static unsafe IRawBitmap CreateImageFromBitmap(IntPtr pSource, int offset, ImageDescription description)
        {
            var originalFormat = description.Format;
            var realFormatSize = description.Format.SizeOfInBytes();
            var sizeInBytes = description.Format.SizeOfInBytes();
            var bufferSize = description.Width * description.Height * sizeInBytes;
            byte[] buffer = new byte[bufferSize];
            var rowStride = (int)(description.Width * realFormatSize);
            var alignedRowStride = AlignStride(rowStride);
            int rowStrideDiff = alignedRowStride - rowStride;
            var conversionFlags = ConversionFlags.None;
            if (realFormatSize == 2)
            {
                conversionFlags = originalFormat == Format.B5G5R5A1_UNORM_PACK16 ? ConversionFlags.RGB555 : ConversionFlags.RGB565;
            }

            using (var stream = new UnmanagedMemoryStream((byte*)pSource, rowStride * description.Height + offset))
            {
                int bufferOffset = 0;
                var streamOffset = 0;
                stream.Seek(offset, SeekOrigin.Begin);
                for (int i = 0; i < bufferSize; i += sizeInBytes)
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
                        if (conversionFlags == ConversionFlags.RGB565)
                        {
                            b = (byte)(rg16 & ColorMasks.RGB565.BlueMask);
                            g = (byte)((rg16 & ColorMasks.RGB565.GreenMask) >> 5);
                            r = (byte)((rg16 & ColorMasks.RGB565.RedMask) >> 11);
                        }
                        else if (conversionFlags == ConversionFlags.RGB555)
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

                    if (realFormatSize < sizeInBytes)
                    {
                        var bytesDiff = sizeInBytes - realFormatSize;
                        bufferOffset += bytesDiff;
                    }

                    if (rowStride % 4 != 0 && streamOffset % rowStride == 0)
                    {
                        var tmpBuffer = new byte[rowStrideDiff];
                        stream.Read(tmpBuffer, 0, rowStrideDiff);
                    }
                }
            }

            var bmpImage = new BmpImage(description);
            buffer = ImageHelper.FlipBuffer(
                buffer, 
                description.Width, 
                description.Height, 
                description.Format.SizeOfInBytes(),
                FlipBufferOptions.FlipVertically);

            if (description.Format.SizeOfInBytes() == 4)
            {
                ImageHelper.SetAlpha(buffer, 255);
            }

            bmpImage.PixelData = buffer;
            return bmpImage;
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

        public static void SaveToStream(IRawBitmap img, Stream imageStream)
        {
            SaveToBitmapStream(img, imageStream);
        }

        private static unsafe void SaveToBitmapStream(IRawBitmap rawBitmap, Stream imageStream)
        {
            imageStream.Seek(0, SeekOrigin.Begin);
            var frameData = rawBitmap.GetFrameData(0);

            uint offsetData = (uint)(Marshal.SizeOf<BitmapFileHeader>() + Marshal.SizeOf<BitmapInfoHeader>()); //+ Marshal.SizeOf<BitmapColorHeader>());
            uint fileSize = offsetData;

            if (frameData.Description.RowStride % 4 == 0)
            {
                fileSize += (uint)frameData.BufferSize;
            }
            else
            {
                var width = AlignStride((int)rawBitmap.Width);
                var size = width * rawBitmap.Height * rawBitmap.PixelFormat.SizeInBytes;
                fileSize += (uint)size;
            }

            var fileHeader = new BitmapFileHeader();
            fileHeader.fileType = fileType;
            fileHeader.fileSize = fileSize;
            fileHeader.offsetData = offsetData;

            var infoHeader = new BitmapInfoHeader();
            infoHeader.bitCount = (ushort)rawBitmap.PixelFormat.SizeInBits;
            infoHeader.width = (int)rawBitmap.Width;
            infoHeader.height = (int)rawBitmap.Height;
            infoHeader.planes = 1;
            infoHeader.size = (uint)Marshal.SizeOf<BitmapInfoHeader>();

            var buffer = new byte[fileSize];
            IntPtr fileHeaderMemoryPtr = Utilities.AllocateMemory(Marshal.SizeOf<BitmapFileHeader>(), 2);
            Marshal.StructureToPtr(fileHeader, fileHeaderMemoryPtr, true);
            Utilities.Read(fileHeaderMemoryPtr, buffer, 0, Marshal.SizeOf<BitmapFileHeader>());

            IntPtr infoHeaderMemoryPtr = Utilities.AllocateMemory(Marshal.SizeOf<BitmapInfoHeader>());
            Marshal.StructureToPtr(infoHeader, infoHeaderMemoryPtr, true);
            Utilities.Read(infoHeaderMemoryPtr, buffer, Marshal.SizeOf<BitmapFileHeader>(), Marshal.SizeOf<BitmapInfoHeader>());

            IntPtr colorHeaderMemoryPtr = Utilities.AllocateMemory(Marshal.SizeOf<BitmapColorHeader>());
            Marshal.StructureToPtr(infoHeader, colorHeaderMemoryPtr, true);
            var bufferOffset = Marshal.SizeOf<BitmapFileHeader>() + Marshal.SizeOf<BitmapInfoHeader>();
            Utilities.Read(colorHeaderMemoryPtr, buffer, bufferOffset, Marshal.SizeOf<BitmapInfoHeader>());

            var colors = ImageHelper.FlipBuffer(
                frameData.RawPixels, 
                frameData.Description.Width,
                frameData.Description.Height,
                frameData.Description.Format.SizeOfInBytes(),
                FlipBufferOptions.FlipVertically);
            var pixelSize = rawBitmap.PixelFormat.SizeInBytes;
            for (int i = 0; i < colors.Length; i+=pixelSize)
            {
                Utilities.Swap(ref colors[i], ref colors[i+2]);
            }
            
            imageStream.Write(buffer, 0, (int)offsetData);
            var handle = GCHandle.Alloc(colors, GCHandleType.Pinned);
            Utilities.Read(handle.AddrOfPinnedObject(), buffer, bufferOffset, colors.Length);
            handle.Free();
            
            if (frameData.Description.RowStride % 4 == 0)
            {
                imageStream.Write(buffer, (int)offsetData, colors.Length);
            }
            else
            {
                var alignedWidth = AlignStride((int)frameData.Description.Width);
                var alignDiff = alignedWidth - frameData.Description.Width;
                int rowStride = (int)(frameData.Description.Width * frameData.Description.Format.SizeOfInBytes());
                for (int i = 0; i < frameData.Description.Height; ++i)
                {
                    imageStream.Write(buffer, (int)offsetData, rowStride);
                    for (int k = 0; k < alignDiff; ++k)
                    {
                        imageStream.WriteByte(0);
                    }
                    offsetData += (uint)rowStride;
                }
            }
        }
    };
}
