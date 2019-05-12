using Adamantium.Core;
using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using static Adamantium.Engine.Graphics.BitmapHelper;

namespace Adamantium.Engine.Graphics
{
    public static class ICOHelper
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct IconDir
        {
            public ushort Reserved;   // Reserved
            public ushort Type;       // resource type (1 for icons)
            public ushort Count;      // how many images?
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct IconInfo
        {
            public byte Width;              // Width of the image
            public byte Height;             // Height of the image (times 2)
            public byte ColorCount;         // Number of colors in image (0 if >=8bpp)
            public byte Reserved;           // Reserved
            public ushort Planes;           // Color Planes
            public ushort BitCount;         // Bits per pixel
            public uint BytesInRes;         // how many bytes in this resource?
            public uint ImageOffset;        // where in the file is this image
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct IconImageInfo
        {
            public BitmapInfoHeader Header;  // DIB header
            // Color table (short 4 bytes) //RGBQUAD
            public byte R;
            public byte G;
            public byte B;
            public byte reserved;
            public byte XOR;      // DIB bits for XOR mask
            public byte AND;      // DIB bits for AND mask
        }

        public static Image LoadFromICOMemory(IntPtr pSource, int size, bool makeACopy, GCHandle? handle)
        {
            if (!DecodeICOHeader(pSource, size, out var description, out List<IconInfo> iconsInfos))
            {
                return null;
            }

            return CreateImageFromICO(pSource, description, handle, iconsInfos);
        }

        private static unsafe bool DecodeICOHeader(IntPtr headerPtr, int size, out ImageDescription description, out List<IconInfo> iconsInfos)
        {
            description = new ImageDescription();
            iconsInfos = new List<IconInfo>();

            var header = *(IconDir*)headerPtr;

            if (header.Reserved != 0 || header.Count == 0)
            {
                return false;
            }

            var dataPtr = IntPtr.Add(headerPtr, Marshal.SizeOf<IconDir>());
            for (int i = 0; i< header.Count; ++i)
            {
                var entry = *(IconInfo*)dataPtr;
                dataPtr = IntPtr.Add(dataPtr, Marshal.SizeOf<IconInfo>());
                iconsInfos.Add(entry);
            }

            return true;
        }

        private static unsafe Image CreateImageFromICO(IntPtr pSource, ImageDescription description, GCHandle? handle, List<IconInfo> iconsInfos)
        {
            //Find image with max resolution
            IconInfo iconInfo = iconsInfos[0];
            int maxResolution = iconInfo.Width * iconInfo.Height;
            int maxBitsPerPixel = iconInfo.BitCount;
            foreach (var info in iconsInfos)
            {
                if (info.Width * info.Height > maxResolution)
                {
                    maxResolution = info.Width * info.Height;
                    maxBitsPerPixel = iconInfo.BitCount;
                    iconInfo = info;
                }
            }

            var dataPtr = IntPtr.Add(pSource, (int)iconInfo.ImageOffset);

            var iconImageInfo = *(IconImageInfo*)dataPtr;
            var width = iconImageInfo.Header.width;
            var height = iconImageInfo.Header.height;
            if (height == width * 2)
            {
                height /= 2;
            }

            int realBitsCount = (int)iconImageInfo.Header.bitCount;
            bool hasAndMask = /*(realBitsCount < 32) &&*/ (height != iconImageInfo.Header.height);

            dataPtr = IntPtr.Add(dataPtr, Marshal.SizeOf<IconImageInfo>());
            var buffer = new byte[width * height * 4];

            var stream = new UnmanagedMemoryStream((byte*)dataPtr, iconInfo.BytesInRes);
            var sizeinBytes = realBitsCount / 8;
            int bufferOffset = 0;
            if (realBitsCount == 32)
            {
                for (int i = 0; i < buffer.Length; i += sizeinBytes)
                {
                    stream.Read(buffer, bufferOffset, sizeinBytes);
                    Utilities.Swap(ref buffer[bufferOffset], ref buffer[bufferOffset + 2]);
                    //buffer[bufferOffset] = (byte)(buffer[bufferOffset] & iconImageInfo.R);
                    //buffer[bufferOffset + 1] = (byte)(buffer[bufferOffset + 1] & iconImageInfo.G);
                    //buffer[bufferOffset + 2] = (byte)(buffer[bufferOffset + 2] & iconImageInfo.B);
                    //buffer[bufferOffset + 3] = (byte)(buffer[bufferOffset + 3] & 0xff);
                    bufferOffset += sizeinBytes;
                }
            }
            else if (realBitsCount == 24)
            {
                for (int i = 0; i < buffer.Length; i += 4)
                {
                    stream.Read(buffer, bufferOffset, sizeinBytes);
                    Utilities.Swap(ref buffer[bufferOffset], ref buffer[bufferOffset + 2]);
                    //buffer[bufferOffset] = (byte)(buffer[bufferOffset] ^ iconImageInfo.XOR);
                    //buffer[bufferOffset + 1] = (byte)(buffer[bufferOffset + 1] ^ iconImageInfo.XOR);
                    //buffer[bufferOffset + 2] = (byte)(buffer[bufferOffset + 2] ^ iconImageInfo.XOR);
                    bufferOffset += 4;
                }
            }
            else if (realBitsCount == 4)
            {
                var colors = new byte[16 * 4];
                stream.Read(colors, 0, colors.Length);
                var cursor = IntPtr.Add(dataPtr, colors.Length);
                int shift;
                int shift2;
                byte index;
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        shift = 4 * (x + y * width);
                        //shift2 = (x + height * width);
                        shift2 = (x + (height - y - 1) * width);
                        index = (byte)(void*)IntPtr.Add(cursor, shift2 / 2);
                        if (shift2 % 2 == 0)
                            index = (byte)((index >> 4) & 0xF);
                        else
                            index = (byte)(index & 0xF);
                        index *= 4;

                        buffer[shift] = colors[index + 2];
                        buffer[shift + 1] = colors[index + 1];
                        buffer[shift + 2] = colors[index];
                        buffer[shift + 3] = 255;
                    }
                }
            }

            // Read AND mask after base color data - 1 BIT MASK
            if (hasAndMask)
            {
                int shift;
                int shift2;
                byte bit;
                int mask;

                int boundary = width * realBitsCount; //!!! 32 bit boundary (http://www.daubnet.com/en/file-format-ico)
                while (boundary % 32 != 0) boundary++;
                dataPtr += boundary * height / 8;

                boundary = width;
                while (boundary % 32 != 0) boundary++;

                var bufSize = iconInfo.BytesInRes - (width * height * (realBitsCount / 8));
                var transparencyBuffer = new byte[bufSize];
                stream.Read(transparencyBuffer, 0, (int)bufSize);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        shift = 4 * (x + y * width) + 3;
                        bit = (byte)(7 - (x % 8));
                        //shift2 = (x + (height - y - 1) * boundary) / 8;
                        shift2 = (x + y * boundary) / 8;
                        var b = transparencyBuffer[shift2];
                        mask = (0x01 & (b >> bit));
                        buffer[shift] *= (byte)(1 - mask);
                        //buffer[shift] = b;
                    }
                }
                var str = string.Join(',', transparencyBuffer);
                Debug.WriteLine(str);
            }

            stream.Dispose();

            description.Width = width;
            description.Height = height;
            description.Format = Format.R8G8B8A8_UNORM;
            description.MipLevels = 1;
            description.ArraySize = 1;
            description.Depth = 1;

            Image image = Image.New(description);
            var bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            var ptr = image.PixelBuffer[0].DataPointer;
            Utilities.CopyMemory(ptr, bufferHandle.AddrOfPinnedObject(), buffer.Length);
            bufferHandle.Free();
            var px = PixelBuffer.FlipBuffer(image.PixelBuffer[0], FlipBufferOptions.FlipVertically);
            image.ApplyPixelBuffer(px, 0, true);

            return image;
        }

        public static unsafe void SaveToICOStream(PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {

        }
    }
}
