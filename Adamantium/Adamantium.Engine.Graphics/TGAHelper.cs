using System;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Core;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    internal class TGAHelper
    {
        public enum TGAImageType : byte
        {
            NoImage = 0,
            ColorMapped = 1,
            TrueColor = 2,
            BlackAndWhite = 3,
            ColorMappedRLE = 9,
            TruecolorRLE = 10,
            BlackAndWhiteRLE = 11,
        }

        [Flags]
        public enum TGADescriptorFlags : byte
        {
            InvertX = 0x10,
            InvertY = 0x20,
            Interleaved2Way = 0x40, //Deprecated
            Interleaved4Way = 0x80, // Deprecated
        }

        [Flags]
        public enum TGAConversionFlags
        {
            None = 0x0,
            Expand = 0x1,        //Conversion requires expanded pixel size
            InvertX = 0x2,       //If set, scanlines are right to left
            InvertY = 0x4,       //If set, scanlinew are top to bottom
            RLE = 0x8,           //Source data is RLE compressed
            Swizzle = 0x10000,   //Swizzle BGR<->RGB data
            Format888 = 0x20000  // 24bpp format
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TGAHeader
        {
            public byte IDLength;
            public byte ColorMapType;
            public byte ImageType;
            public ushort ColorMapFirst;
            public ushort ColorMapLength;
            public byte ColorMapSize;
            public ushort XOrigin;
            public ushort YOrigin;
            public ushort Width;
            public ushort Height;
            public byte BitsPerPixel;
            public byte Descriptor;
        }

        public static unsafe bool DecodeTGAHeader(IntPtr source, int size, out ImageDescription description, out int offset, out TGAConversionFlags convFlags)
        {
            if (source == IntPtr.Zero)
            {
                throw new ArgumentException("Source cannot be IntPtr.Zero");
            }

            if (size < Utilities.SizeOf<TGAHeader>())
            {
                throw new ArgumentException("File contains invalid data");
            }

            description = new ImageDescription();
            offset = 0;
            convFlags = TGAConversionFlags.None;

            var header = (TGAHeader*)source;

            if (header->ColorMapType != 0 || header->ColorMapLength != 0)
            {
                return false;
            }

            if (((TGADescriptorFlags)header->Descriptor).HasFlag(TGADescriptorFlags.Interleaved2Way | TGADescriptorFlags.Interleaved4Way))
            {
                return false;
            }

            if (header->Width == 0 || header->Height == 0)
            {
                return false;
            }

            switch ((TGAImageType)header->ImageType)
            {
                case TGAImageType.TrueColor:
                case TGAImageType.TruecolorRLE:
                    switch (header->BitsPerPixel)
                    {
                        case 16:
                            description.Format = Format.B5G5R5A1_UNORM_PACK16;
                            break;
                        case 24:
                            description.Format = Format.R8G8B8A8_UNORM;
                            convFlags |= TGAConversionFlags.Expand;
                            // We could use DXGI_FORMAT_B8G8R8X8_UNORM, but we prefer DXGI 1.0 formats
                            break;
                        case 32:
                            description.Format = Format.R8G8B8A8_UNORM;
                            // We could use DXGI_FORMAT_B8G8R8A8_UNORM, but we prefer DXGI 1.0 formats
                            break;
                    }

                    if ((TGAImageType)header->ImageType == TGAImageType.TruecolorRLE)
                    {
                        convFlags |= TGAConversionFlags.RLE;
                    }

                    break;
                case TGAImageType.BlackAndWhite:
                case TGAImageType.BlackAndWhiteRLE:
                    switch (header->BitsPerPixel)
                    {
                        case 8:
                            description.Format = Format.R8_UNORM;
                            break;

                        default:
                            return false;
                    }

                    if ((TGAImageType)header->ImageType == TGAImageType.BlackAndWhiteRLE)
                    {
                        convFlags |= TGAConversionFlags.RLE;
                    }
                    break;

                case TGAImageType.NoImage:
                case TGAImageType.ColorMapped:
                case TGAImageType.ColorMappedRLE:
                    return false;

                default:
                    return false;
            }

            description.Width = header->Width;
            description.Height = header->Height;
            description.Depth = description.ArraySize = description.MipLevels = 1;
            description.Dimension = TextureDimension.Texture2D;

            if ((header->Descriptor & (byte)TGADescriptorFlags.InvertX) != 0)
            {
                convFlags |= TGAConversionFlags.InvertX;
            }

            if ((header->Descriptor & (byte)TGADescriptorFlags.InvertY) != 0)
            {
                convFlags |= TGAConversionFlags.InvertY;
            }

            offset = Utilities.SizeOf<TGAHeader>();

            if (header->IDLength != 0)
            {
                offset += header->IDLength;
            }

            return true;
        }

        public static bool EncodeTGAHeader(ImageDescription description, out TGAHeader header, IntPtr pDestination, ref TGAConversionFlags flags)
        {
            header = new TGAHeader();
            if (description.Width > 0xFFFF || description.Height > 0xFFFF)
            {
                return false;
            }

            header.Width = (ushort)description.Width;
            header.Height = (ushort)description.Height;

            switch (description.Format)
            {
                case Format.R8G8B8A8_UNORM:
                case Format.R8G8B8A8_SRGB:
                    header.ImageType = (byte)TGAImageType.TrueColor;
                    header.BitsPerPixel = 32;
                    header.Descriptor = (byte)TGADescriptorFlags.InvertY | 8;
                    flags |= TGAConversionFlags.Swizzle;
                    break;
                case Format.B8G8R8A8_UNORM:
                case Format.B8G8R8A8_SRGB:
                    header.ImageType = (byte)TGAImageType.TrueColor;
                    header.BitsPerPixel = 32;
                    header.Descriptor = (byte)TGADescriptorFlags.InvertY | 8;
                    flags |= TGAConversionFlags.Swizzle;
                    break;
                case Format.R8G8B8_UNORM:
                    header.ImageType = (byte)TGAImageType.TrueColor;
                    header.BitsPerPixel = 24;
                    header.Descriptor = (byte)TGADescriptorFlags.InvertY;
                    flags |= TGAConversionFlags.Format888 | TGAConversionFlags.Swizzle;
                    break;
                case Format.B8G8R8_UNORM:
                case Format.B8G8R8_SRGB:
                    header.ImageType = (byte)TGAImageType.TrueColor;
                    header.BitsPerPixel = 24;
                    header.Descriptor = (byte)TGADescriptorFlags.InvertY;
                    flags |= TGAConversionFlags.Format888;
                    break;
                case Format.R8_UNORM:
                    header.ImageType = (byte)TGAImageType.BlackAndWhite;
                    header.BitsPerPixel = 8;
                    header.Descriptor = (byte)TGADescriptorFlags.InvertY;
                    break;
                case Format.B5G5R5A1_UNORM_PACK16:
                    header.ImageType = (byte)TGAImageType.TrueColor;
                    header.BitsPerPixel = 16;
                    header.Descriptor = (byte)TGADescriptorFlags.InvertY | 1;
                    break;

                default:
                    return false;
            }

            return true;
        }

        public static Image LoadFromTgaMemory(IntPtr source, int size, bool makeACopy, GCHandle? handle)
        {
            int offset = 0;
            TGAConversionFlags conversionFlags = 0;
            ImageDescription description;
            var result = DecodeTGAHeader(source, size, out description, out offset, out conversionFlags);

            if (result == false)
            {
                return null;
            }

            if (offset > size)
            {
                return null;
            }

            var image = CreateImageFromTGA(source, offset, description, conversionFlags, handle);

            return image;
        }

        private static Image CreateImageFromTGA(IntPtr pTGA, int offset, ImageDescription description,
           TGAConversionFlags convFlags, GCHandle? handle)
        {
            Image image = new Image(description, pTGA, offset, handle, true);
            Image newImage;

            //If TGA compressed, then we must uncompress pixels
            if (convFlags.HasFlag(TGAConversionFlags.RLE))
            {
                newImage = UncompressPixels(image, convFlags);
            }
            else
            {
                newImage = CopyPixels(image, convFlags);
            }

            image.Dispose();

            return newImage;
        }

        internal static unsafe bool SetAlphaChannelToOpaque(Image dstImage)
        {
            var dstPixels = dstImage.PixelBuffer[0].DataPointer;

            int dpitch = dstImage.PixelBuffer[0].RowStride;

            for (int i = 0; i < dstImage.Description.Height; ++i)
            {
                ImageHelper.CopyScanline(dstPixels, dpitch, dstPixels, dpitch, dstImage.Description.Format, ImageHelper.ScanlineFlags.SetAlpha);
                dstPixels = (IntPtr)((byte*)dstPixels + dpitch);
            }

            return true;
        }

        public static void SaveToTgaMemory(PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            SaveToTgaStream(pixelBuffers, count, description, imageStream);
        }

        public static unsafe void SaveToTgaStream(PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            TGAHeader header;
            TGAConversionFlags flags = TGAConversionFlags.None;
            EncodeTGAHeader(description, out header, pixelBuffers[0].DataPointer, ref flags);

            int rowPitch, slicePitch;
            if (flags.HasFlag(TGAConversionFlags.Format888))
            {
                rowPitch = description.Width * 3;
                slicePitch = description.Height * rowPitch;
            }
            else
            {
                int widthCount, heightCount;
                Image.ComputePitch(description.Format, description.Width, description.Height, out rowPitch, out slicePitch, out widthCount, out heightCount);
            }

            IntPtr pSource = pixelBuffers[0].DataPointer;
            int sPitch = pixelBuffers[0].RowStride;

            int headerSize = Utilities.SizeOf<TGAHeader>();
            var buffer = new byte[Math.Max(slicePitch, headerSize)];
            IntPtr memory = Utilities.AllocateMemory(headerSize, 1);
            Marshal.StructureToPtr(header, memory, true);
            Utilities.Read(memory, buffer, 0, headerSize);
            imageStream.Write(buffer, 0, headerSize);
            Utilities.FreeMemory(memory);

            var imgDst = new Image(description, IntPtr.Zero, 0, null, true);
            var dPtr = imgDst.PixelBuffer[0].DataPointer;

            for (int y = 0; y < description.Height; ++y)
            {
                if (flags.HasFlag(TGAConversionFlags.Format888))
                {
                    ImageHelper.CopyScanline(dPtr, pSource, rowPitch);
                }
                else if (flags.HasFlag(TGAConversionFlags.Swizzle))
                {
                    ImageHelper.SwizzleScanline(dPtr, rowPitch, pSource, sPitch,
                       description.Format, ImageHelper.ScanlineFlags.None);
                }
                else
                {
                    ImageHelper.CopyScanline(dPtr, rowPitch, pSource, sPitch,
                       description.Format, ImageHelper.ScanlineFlags.None);
                }

                dPtr = (IntPtr)((byte*)dPtr + rowPitch);
                pSource = (IntPtr)((byte*)pSource + sPitch);
            }

            //renew pointer address as far as it was moved by code above
            dPtr = imgDst.PixelBuffer[0].DataPointer;

            Utilities.Read(dPtr, buffer, 0, slicePitch);
            imageStream.Write(buffer, 0, slicePitch);
            imgDst.Dispose();
        }

        private static unsafe Image UncompressPixels(Image image, TGAConversionFlags convFlags)
        {
            ImageDescription description = image.Description;

            int rowPitch = 0;
            int slicePitch = 0;
            if (convFlags.HasFlag(TGAConversionFlags.Expand))
            {
                rowPitch = description.Width * 3;
            }
            else
            {
                int newWidth;
                int newHeight;
                Image.ComputePitch(description.Format, description.Width, description.Height, out rowPitch, out slicePitch, out newWidth, out newHeight);
            }


            var imgDst = new Image(description, IntPtr.Zero, 0, null, true);
            IntPtr pDestination = imgDst.PixelBuffer[0].DataPointer;

            int rowStride = imgDst.PixelBuffer[0].RowStride;
            int size = image.PixelBuffer[0].BufferStride;

            IntPtr pSource = image.PixelBuffer[0].DataPointer;
            var sPtr = (byte*)pSource;
            var endPtr = sPtr + size;

            switch (description.Format)
            {
                //------------------------------------------------- 8 bit
                case Format.R8_UNORM:

                    for (int y = 0; y < description.Height; ++y)
                    {
                        int offset = convFlags.HasFlag(TGAConversionFlags.InvertX) ? description.Width - 1 : 0;

                        var dPtr = ((byte*)pDestination +
                                            (rowStride *
                                             (convFlags.HasFlag(TGAConversionFlags.InvertY) ? y : (description.Height - y - 1)))) +
                                   offset;

                        for (int x = 0; x < description.Width;)
                        {
                            if (sPtr >= endPtr)
                            {
                                throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                            }

                            if ((*sPtr & 0x80) != 0)
                            {
                                int j = (*sPtr & 0x7F) + 1;

                                if (++sPtr >= endPtr)
                                {
                                    throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                }

                                for (; j > 0; --j, ++x)
                                {
                                    if (x >= description.Width)
                                    {
                                        throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                    }

                                    *dPtr = *sPtr;

                                    if (convFlags.HasFlag(TGAConversionFlags.InvertX))
                                    {
                                        --dPtr;
                                    }
                                    else
                                    {
                                        ++dPtr;
                                    }
                                }

                                ++sPtr;
                            }
                            else
                            {
                                int j = (*sPtr & 0x7F) + 1;

                                if (sPtr + j > endPtr)
                                {
                                    throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                }

                                for (; j > 0; --j, ++x)
                                {
                                    if (x >= description.Width)
                                    {
                                        throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                    }

                                    *dPtr = *(sPtr++);

                                    if (convFlags.HasFlag(TGAConversionFlags.InvertX))
                                    {
                                        --dPtr;
                                    }
                                    else
                                    {
                                        ++dPtr;
                                    }
                                }
                            }
                        }
                    }
                    break;

                case Format.B5G5R5A1_UNORM_PACK16:
                    bool nonzeroa = false;
                    for (int y = 0; y < description.Height; ++y)
                    {
                        int offset = convFlags.HasFlag(TGAConversionFlags.InvertX) ? description.Width - 1 : 0;

                        var dPtr = (ushort*)((byte*)pDestination +
                                              (rowStride *
                                               (convFlags.HasFlag(TGAConversionFlags.InvertY)
                                                  ? y
                                                  : (description.Height - y - 1)))) +
                                   offset;

                        for (int x = 0; x < description.Width;)
                        {
                            if (sPtr >= endPtr)
                            {
                                throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                            }

                            if ((*sPtr & 0x80) != 0)
                            {
                                int j = (*sPtr & 0x7F) + 1;

                                if (++sPtr >= endPtr)
                                {
                                    throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                }

                                ushort t = (ushort)(*sPtr | (*(sPtr + 1) << 8));

                                if ((t & 0x80) != 0)
                                {
                                    nonzeroa = true;
                                }
                                sPtr += 2;

                                for (; j > 0; --j, ++x)
                                {
                                    if (x >= description.Width)
                                    {
                                        throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                    }

                                    *dPtr = t;

                                    if (convFlags.HasFlag(TGAConversionFlags.InvertX))
                                    {
                                        --dPtr;
                                    }
                                    else
                                    {
                                        ++dPtr;
                                    }
                                }
                            }
                            else
                            {
                                int j = (*sPtr & 0x7F) + 1;
                                ++sPtr;

                                if (sPtr + (j * 2) > endPtr)
                                {
                                    throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                }

                                for (; j > 0; --j, ++x)
                                {
                                    if (x >= description.Width)
                                    {
                                        throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                    }
                                    ushort t = (ushort)(*sPtr | (*(sPtr + 1) << 8));
                                    if ((t & 0x80) != 0)
                                    {
                                        nonzeroa = true;
                                    }
                                    sPtr += 2;
                                    *dPtr = t;

                                    if (convFlags.HasFlag(TGAConversionFlags.InvertX))
                                    {
                                        --dPtr;
                                    }
                                    else
                                    {
                                        ++dPtr;
                                    }
                                }
                            }
                        }
                    }

                    if (!nonzeroa)
                    {
                        SetAlphaChannelToOpaque(image);
                    }
                    break;

                case Format.R8G8B8A8_UNORM:
                    nonzeroa = false;
                    for (int y = 0; y < description.Height; ++y)
                    {
                        int offset = convFlags.HasFlag(TGAConversionFlags.InvertX) ? description.Width - 1 : 0;

                        var dPtr = (uint*)((byte*)pDestination +
                                            (rowStride *
                                             (convFlags.HasFlag(TGAConversionFlags.InvertY) ? y : (description.Height - y - 1)))) +
                                   offset;

                        for (int x = 0; x < description.Width;)
                        {
                            if ((*sPtr & 0x80) == 0x80)
                            {
                                var j = (*sPtr & 0x7F) + 1;
                                ++sPtr;

                                uint t;
                                if (convFlags.HasFlag(TGAConversionFlags.Expand))
                                {
                                    if (sPtr + 2 >= endPtr)
                                    {
                                        throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                    }

                                    // BGR -> RGBA
                                    t = (uint)((*sPtr << 16) | (*(sPtr + 1) << 8) | (*(sPtr + 2)) | 0xFF000000);
                                    sPtr += 3;

                                    nonzeroa = true;
                                }
                                else
                                {
                                    if (sPtr + 3 >= endPtr)
                                    {
                                        throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                    }

                                    // BGRA -> RGBA
                                    t = (uint)((*sPtr << 16) | (*(sPtr + 1) << 8) | (*(sPtr + 2)) | (*(sPtr + 3) << 24));

                                    if (*(sPtr + 3) > 0)
                                    {
                                        nonzeroa = true;
                                    }

                                    sPtr += 4;
                                }

                                for (; j > 0; --j, ++x)
                                {
                                    if (x >= description.Width)
                                    {
                                        throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                    }

                                    *dPtr = t;

                                    if (convFlags.HasFlag(TGAConversionFlags.InvertX))
                                    {
                                        --dPtr;
                                    }
                                    else
                                    {
                                        ++dPtr;
                                    }
                                }
                            }
                            else
                            {
                                int j = (*sPtr & 0x7F) + 1;
                                ++sPtr;

                                if (convFlags.HasFlag(TGAConversionFlags.Expand))
                                {
                                    if (sPtr + (j * 3) > endPtr)
                                    {
                                        throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                    }
                                }
                                else
                                {
                                    if (sPtr + (j * 4) > endPtr)
                                    {
                                        throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                    }
                                }

                                for (; j > 0; --j, ++x)
                                {
                                    if (x >= description.Width)
                                    {
                                        throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                    }

                                    if (convFlags.HasFlag(TGAConversionFlags.Expand))
                                    {
                                        if (sPtr + 2 >= endPtr)
                                        {
                                            throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                        }

                                        //BGR -> RGBA
                                        *dPtr = (uint)((*sPtr << 16) | (*(sPtr + 1) << 8) | (*(sPtr + 2)) | 0xFF000000);
                                        sPtr += 3;

                                        nonzeroa = true;
                                    }
                                    else
                                    {
                                        if (sPtr + 3 >= endPtr)
                                        {
                                            return null;
                                        }

                                        // BGRA -> RGBA
                                        *dPtr =
                                           (uint)((*sPtr << 16) | (*(sPtr + 1) << 8) | (*(sPtr + 2)) | (*(sPtr + 3) << 24));

                                        if (*(sPtr + 3) > 0)
                                        {
                                            nonzeroa = true;
                                        }

                                        sPtr += 4;
                                    }

                                    if (convFlags.HasFlag(TGAConversionFlags.InvertX))
                                    {
                                        --dPtr;
                                    }
                                    else
                                    {
                                        ++dPtr;
                                    }
                                }
                            }
                        }
                    }

                    if (!nonzeroa)
                    {
                        SetAlphaChannelToOpaque(image);
                    }
                    break;
            }

            return imgDst;
        }

        private static unsafe Image CopyPixels(Image img, TGAConversionFlags convFlags)
        {
            ImageDescription description = img.Description;
            int rowPitch, slicePitch;
            if (convFlags.HasFlag(TGAConversionFlags.Format888))
            {
                rowPitch = description.Width * 3;
            }
            else
            {
                int widthCount, heightCount;
                Image.ComputePitch(description.Format, description.Width, description.Height, out rowPitch, out slicePitch,
                   out widthCount, out heightCount);
            }

            var imgDst = new Image(description, IntPtr.Zero, 0, null, true);
            IntPtr pDestination = imgDst.PixelBuffer[0].DataPointer;
            IntPtr pSource = img.PixelBuffer[0].DataPointer;
            int bufferSize = img.PixelBuffer[0].BufferStride;
            int rowStride = imgDst.PixelBuffer[0].RowStride;
            var sPtr = (byte*)pSource;
            var endPtr = sPtr + bufferSize;

            switch (description.Format)
            {
                case Format.R8_UNORM: //-------------------------- 8 bit
                    for (int y = 0; y < description.Height; ++y)
                    {
                        uint offset = (uint)(convFlags.HasFlag(TGAConversionFlags.InvertX) ? description.Width - 1 : 0);

                        var dPtr = ((byte*)pDestination +
                                            (rowStride * (convFlags.HasFlag(TGAConversionFlags.InvertY) ? y : (description.Height - y - 1))))
                                   + offset;
                        for (int x = 0; x < description.Width; ++x)
                        {
                            *dPtr = *(sPtr++);
                            if (convFlags.HasFlag(TGAConversionFlags.InvertX))
                            {
                                --dPtr;
                            }
                            else
                            {
                                ++dPtr;
                            }
                        }
                    }
                    break;

                case Format.B5G5R5A1_UNORM_PACK16: //-------------------------- 16 bit
                    bool nonzeroa = false;
                    for (int y = 0; y < description.Height; ++y)
                    {
                        uint offset = (uint)(convFlags.HasFlag(TGAConversionFlags.InvertX) ? description.Width - 1 : 0);

                        var dPtr = (ushort*)((byte*)pDestination +
                                            (rowStride * (convFlags.HasFlag(TGAConversionFlags.InvertY) ? y : (description.Height - y - 1))))
                                   + offset;
                        for (int x = 0; x < description.Width; ++x)
                        {
                            if (sPtr + 1 >= endPtr)
                            {
                                throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                            }

                            ushort t = (ushort)(*sPtr | (*(sPtr + 1) << 8));
                            sPtr += 2;
                            *dPtr = t;

                            if ((t & 0x8000) != 0)
                            {
                                nonzeroa = true;
                            }

                            if (convFlags.HasFlag(TGAConversionFlags.InvertX))
                            {
                                --dPtr;
                            }
                            else
                            {
                                ++dPtr;
                            }
                        }
                    }

                    if (!nonzeroa)
                    {
                        SetAlphaChannelToOpaque(imgDst);
                    }
                    break;


                case Format.R8G8B8A8_UNORM: //----------------- 24/32 bit
                    nonzeroa = false;
                    for (int y = 0; y < description.Height; ++y)
                    {
                        uint offset = (uint)(convFlags.HasFlag(TGAConversionFlags.InvertX) ? description.Width - 1 : 0);

                        var dPtr = (uint*)((byte*)pDestination + (rowStride * (convFlags.HasFlag(TGAConversionFlags.InvertY) ? y : (description.Height - y - 1)))) + offset;

                        for (int x = 0; x < description.Width; ++x)
                        {
                            if (convFlags.HasFlag(TGAConversionFlags.Expand))
                            {

                                if (sPtr + 2 >= endPtr)
                                {
                                    throw new InvalidOperationException("TGA convertion failed. Unexpected end of buffer");
                                }

                                // BGR -> RGBA
                                *dPtr = (uint)((*sPtr << 16) | (*(sPtr + 1) << 8) | (*(sPtr + 2)) | 0xFF000000);

                                sPtr += 3;
                                nonzeroa = true;
                            }
                            else
                            {
                                // BGRA -> RGBA
                                *dPtr = (uint)((*sPtr << 16) | (*(sPtr + 1) << 8) | (*(sPtr + 2)) | (*(sPtr + 3) << 24));

                                if (*(sPtr + 3) > 0)
                                {
                                    nonzeroa = true;
                                }

                                sPtr += 4;
                            }

                            if (convFlags.HasFlag(TGAConversionFlags.InvertX))
                            {
                                --dPtr;
                            }
                            else
                            {
                                ++dPtr;
                            }
                        }
                    }

                    if (!nonzeroa)
                    {
                        SetAlphaChannelToOpaque(imgDst);
                    }
                    break;
            }

            return imgDst;
        }
    }
}
