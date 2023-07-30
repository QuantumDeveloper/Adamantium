using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Imaging.Bmp;
using Adamantium.Mathematics;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging.Ico
{
    public static class IcoHelper
    {
        public static IRawBitmap LoadFromMemory(IntPtr pSource, long size)
        {
            if (!DecodeIcoHeader(pSource, size, out var description, out List<IconInfo> iconsInfos))
            {
                throw new ArgumentException("Given file is not an ICO file");
            }

            return CreateImageFromIco(pSource, description, iconsInfos.ToArray());
        }

        private static unsafe bool DecodeIcoHeader(IntPtr headerPtr, long size, out ImageDescription description, out List<IconInfo> iconsInfos)
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

            iconsInfos = iconsInfos.Where(x => x.Width > 0 && x.Height > 0).
                OrderByDescending(x => x.BytesInRes).
                ToList();

            return true;
        }

        private static IRawBitmap CreateImageFromIco(IntPtr pSource, ImageDescription description, IconInfo[] iconsInfos)
        {
            var ico = new IcoImage(description);
            for (int i = 0; i < iconsInfos.Length; i++)
            {
                ProcessIcon(pSource, ico, iconsInfos[i], (uint)i);
            }
            
            // description.Width = (uint)width;
            // description.Height = (uint)height;
            // description.Format = Format.R8G8B8A8_UNORM;
            // description.MipLevels = 1;
            // description.ArraySize = 1;
            // description.Depth = 1;
            //
            // Image image = Image.New(description);
            // var bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            // var ptr = image.PixelBuffer[0].DataPointer;
            // Utilities.CopyMemory(ptr, bufferHandle.AddrOfPinnedObject(), buffer.Length);
            // bufferHandle.Free();
            // var px = PixelBuffer.FlipBuffer(image.PixelBuffer[0], FlipBufferOptions.FlipVertically);
            // var colorsBuf = px.GetPixels<Color>();
            // image.ApplyPixelBuffer(px, 0, true);

            var data = ico.GetMipLevelData(0);
            ico.Description = data.Description;

            return ico;
        }

        private static unsafe void ProcessIcon(IntPtr pSource, IcoImage image, IconInfo iconInfo, uint mipLevel)
        {
            var dataPtr = IntPtr.Add(pSource, (int)iconInfo.ImageOffset);

            var iconImageInfo = *(IconImageInfo*)dataPtr;
            var width = iconImageInfo.Header.width;
            var height = iconImageInfo.Header.height;
            if (height == width * 2)
            {
                height /= 2;
            }

            var descr = ImageDescription.Default2D((uint)width, (uint)height, SurfaceFormat.R8G8B8A8.UNorm);
            
            int realBitsCount = iconImageInfo.Header.bitCount;
            bool hasAndMask = /*(realBitsCount < 32) &&*/ (height != iconImageInfo.Header.height);

            dataPtr = IntPtr.Add(dataPtr, Marshal.SizeOf<BitmapInfoHeader>());
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
                    bufferOffset += sizeinBytes;
                }
            }
            else if (realBitsCount == 24)
            {
                for (int i = 0; i < buffer.Length; i += 4)
                {
                    stream.Read(buffer, bufferOffset, sizeinBytes);
                    Utilities.Swap(ref buffer[bufferOffset], ref buffer[bufferOffset + 2]);
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
            
            stream.Dispose();
            
            var pixelSize = (byte)(realBitsCount / 8);
            
            var pixels = ImageHelper.FlipBuffer(buffer, descr.Width, descr.Height, pixelSize, FlipBufferOptions.FlipVertically);
            var icoMipData = new FrameData(pixels, descr, mipLevel);
            image.AddMipLevel(icoMipData);
            
            // Read AND mask after base color data - 1 BIT MASK
            //if (hasAndMask)
            //{
            //    int shift;
            //    int shift2;
            //    byte bit;
            //    int mask;

            //    int boundary = width * realBitsCount; //!!! 32 bit boundary (http://www.daubnet.com/en/file-format-ico)
            //    while (boundary % 32 != 0) boundary++;

            //    boundary = width;
            //    while (boundary % 32 != 0) boundary++;

            //    var bufSize = iconImageInfo.Header.sizeImage - (width * height * (realBitsCount / 8));
            //    if (bufSize > 0)
            //    {
            //        var transparencyBuffer = new byte[bufSize];
            //        stream.Read(transparencyBuffer, 0, (int)bufSize);
            //        for (int y = 0; y < height; y++)
            //        {
            //            for (int x = 0; x < width; x++)
            //            {
            //                shift = 4 * (x + y * width) + 3;
            //                bit = (byte)(7 - (x % 8));
            //                shift2 = (x + (height - y - 1) * boundary) / 8;
            //                var b = transparencyBuffer[shift2];
            //                mask = (0x01 & (b >> bit));
            //                var before = buffer[shift];
            //                buffer[shift] *= (byte)(1 - mask);
            //            }
            //        }
            //    }
            //}
        }

        public static unsafe void SaveToStream(IRawBitmap img, Stream imageStream)
        {

        }
    }
}
