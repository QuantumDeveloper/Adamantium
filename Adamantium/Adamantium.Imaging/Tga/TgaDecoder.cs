using System;
using System.Runtime.InteropServices;
using Adamantium.Core;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging.Tga;

public static class TgaDecoder
{
    public static unsafe bool DecodeTgaHeader(IntPtr source, long size, out ImageDescription description, out long offset, out TGAConversionFlags convFlags)
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
    
    public static unsafe TgaImage CreateImageFromTGA(IntPtr pTGA,
        long offset,
        ImageDescription description,
        TGAConversionFlags convFlags)
    {
        var pData = (IntPtr)((byte*)pTGA + offset);
        //If TGA compressed, then we must uncompress pixels
        if (convFlags.HasFlag(TGAConversionFlags.RLE))
        {
            return UncompressPixels(pData, description, convFlags);
        }
 
        return CopyPixels(pData, description, convFlags);
    }
    
    private static unsafe TgaImage CopyPixels(IntPtr pSource,
            ImageDescription description,
            TGAConversionFlags convFlags)
        {
            int rowPitch, slicePitch;
            if (convFlags.HasFlag(TGAConversionFlags.Format888))
            {
                rowPitch = (int)description.Width * 3;
            }
            else
            {
                int widthCount, heightCount;
                Image.ComputePitch(description.Format, (int)description.Width, (int)description.Height, out rowPitch, out slicePitch,
                   out widthCount, out heightCount);
            }
            
            long bufferSize = description.Width * description.Height * description.Format.SizeOfInBytes();
            byte[] pixelBuffer = new byte[bufferSize];
            var handle = GCHandle.Alloc(pixelBuffer, GCHandleType.Pinned);
            IntPtr pDestination = handle.AddrOfPinnedObject();

            long rowStride = description.Width * description.Format.SizeOfInBytes();

            // var imgDst = new Image(description, IntPtr.Zero, 0, null, true);
            // IntPtr pDestination = imgDst.PixelBuffer[0].DataPointer;
            // //IntPtr pSource = img.PixelBuffer[0].DataPointer;
            // int bufferSize = img.PixelBuffer[0].BufferStride;
            // int rowStride = imgDst.PixelBuffer[0].RowStride;
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
                        SetAlphaChannelToOpaque(pDestination, (int)rowStride, description);
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
                        SetAlphaChannelToOpaque(pDestination, (int)rowStride, description);
                    }
                    break;
            }

            handle.Free();
            var tgaImage = new TgaImage(description.Width, description.Height, description.Format);
            tgaImage.PixelBuffer = pixelBuffer;
            return tgaImage;
        }
    
    private static unsafe TgaImage UncompressPixels(IntPtr pSource,
            ImageDescription description,
            TGAConversionFlags convFlags)
        {
            int rowPitch = 0;
            int slicePitch = 0;
            if (convFlags.HasFlag(TGAConversionFlags.Expand))
            {
                rowPitch = (int)description.Width * 3;
            }
            else
            {
                int newWidth;
                int newHeight;
                Image.ComputePitch(description.Format, (int)description.Width, (int)description.Height, out rowPitch, out slicePitch, out newWidth, out newHeight);
            }


            //var imgDst = new Image(description, IntPtr.Zero, 0, null, true);
            //IntPtr pDestination = imgDst.PixelBuffer[0].DataPointer;

            //int rowStride = imgDst.PixelBuffer[0].RowStride;
            //int size = image.PixelBuffer[0].BufferStride;
            long size = description.Width * description.Height * description.Format.SizeOfInBytes();
            byte[] pixelBuffer = new byte[size];
            var handle = GCHandle.Alloc(pixelBuffer, GCHandleType.Pinned);
            IntPtr pDestination = handle.AddrOfPinnedObject();

            //IntPtr pSource = image.PixelBuffer[0].DataPointer;
            long rowStride = description.Width * description.Format.SizeOfInBytes();
            var sPtr = (byte*)pSource;
            var endPtr = sPtr + size;

            switch (description.Format)
            {
                //------------------------------------------------- 8 bit
                case Format.R8_UNORM:

                    for (int y = 0; y < description.Height; ++y)
                    {
                        long offset = convFlags.HasFlag(TGAConversionFlags.InvertX) ? (int)description.Width - 1 : 0;

                        var dPtr = ((byte*)pDestination +
                                            (rowStride *
                                             (convFlags.HasFlag(TGAConversionFlags.InvertY) ? y : (description.Height - y - 1)))) +
                                   offset;

                        for (int x = 0; x < description.Width;)
                        {
                            if (sPtr >= endPtr)
                            {
                                throw new InvalidOperationException("TGA conversion failed. Unexpected end of buffer");
                            }

                            if ((*sPtr & 0x80) != 0)
                            {
                                int j = (*sPtr & 0x7F) + 1;

                                if (++sPtr >= endPtr)
                                {
                                    throw new InvalidOperationException("TGA conversion failed. Unexpected end of buffer");
                                }

                                for (; j > 0; --j, ++x)
                                {
                                    if (x >= description.Width)
                                    {
                                        throw new InvalidOperationException("TGA conversion failed. Unexpected end of buffer");
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
                                    throw new InvalidOperationException("TGA conversion failed. Unexpected end of buffer");
                                }

                                for (; j > 0; --j, ++x)
                                {
                                    if (x >= description.Width)
                                    {
                                        throw new InvalidOperationException("TGA conversion failed. Unexpected end of buffer");
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
                        int offset = convFlags.HasFlag(TGAConversionFlags.InvertX) ? (int)description.Width - 1 : 0;

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
                                throw new InvalidOperationException("TGA conversion failed. Unexpected end of buffer");
                            }

                            if ((*sPtr & 0x80) != 0)
                            {
                                int j = (*sPtr & 0x7F) + 1;

                                if (++sPtr >= endPtr)
                                {
                                    throw new InvalidOperationException("TGA conversion failed. Unexpected end of buffer");
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
                                        throw new InvalidOperationException("TGA conversion failed. Unexpected end of buffer");
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
                                    throw new InvalidOperationException("TGA conversion failed. Unexpected end of buffer");
                                }

                                for (; j > 0; --j, ++x)
                                {
                                    if (x >= description.Width)
                                    {
                                        throw new InvalidOperationException("TGA conversion failed. Unexpected end of buffer");
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
                        SetAlphaChannelToOpaque(pDestination, (int)rowStride, description);
                    }
                    break;

                case Format.R8G8B8A8_UNORM:
                    nonzeroa = false;
                    for (int y = 0; y < description.Height; ++y)
                    {
                        int offset = convFlags.HasFlag(TGAConversionFlags.InvertX) ? (int)description.Width - 1 : 0;

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
                        SetAlphaChannelToOpaque(pDestination, (int)rowStride, description);
                    }
                    break;
            }

            handle.Free();
            var tgaImage = new TgaImage(description.Width, description.Height, description.Format);
            tgaImage.PixelBuffer = pixelBuffer;
            return tgaImage;
        }
    
    private static unsafe bool SetAlphaChannelToOpaque(IntPtr dstPixels, int rowStride, ImageDescription description)
    {
        //var dstPixels = dstImage.PixelBuffer[0].DataPointer;

        //int dpitch = dstImage.PixelBuffer[0].RowStride;

        for (int i = 0; i < description.Height; ++i)
        {
            ImageHelper.CopyScanline(dstPixels, rowStride, dstPixels, rowStride, description.Format, ImageHelper.ScanlineFlags.SetAlpha);
            dstPixels = (IntPtr)((byte*)dstPixels + rowStride);
        }

        return true;
    }
}