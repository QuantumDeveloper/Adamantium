﻿// Copyright (c) 2010-2014 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// -----------------------------------------------------------------------------
// The following code is a port of DirectXTex https://github.com/microsoft/DirectXTex
// -----------------------------------------------------------------------------
// The MIT License (MIT)

//Copyright(c) 2011-2019 Microsoft Corp

//Permission is hereby granted, free of charge, to any person obtaining a copy of this
//software and associated documentation files (the "Software"), to deal in the Software
//without restriction, including without limitation the rights to use, copy, modify, 
//merge, publish, distribute, sublicense, and/or sell copies of the Software, and to
//permit persons to whom the Software is furnished to do so, subject to the following
//conditions: 

//The above copyright notice and this permission notice shall be included in all copies
//or substantial portions of the Software.  

//THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
//INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A
//PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
//HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF
//CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE
//OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Core;
using AdamantiumVulkan.Core;

namespace Adamantium.Imaging.Dds
{
    public class DdsHelper
    {
        /// <summary>
        /// Magic code to identify DDS header
        /// </summary>
        internal const uint MagicHeader = 0x20534444; // "DDS "

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct LegacyMap
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="LegacyMap" /> struct.
            /// </summary>
            /// <param name="format">The format.</param>
            /// <param name="conversionFlags">The conversion flags.</param>
            /// <param name="pixelFormat">The pixel format.</param>
            public LegacyMap(DXGIFormat format, ConversionFlags conversionFlags, PixelFormat pixelFormat)
            {
                DXGIFormat = format;
                ConversionFlags = conversionFlags;
                PixelFormat = pixelFormat;
            }

            public DXGIFormat DXGIFormat;
            public ConversionFlags ConversionFlags;
            public PixelFormat PixelFormat;
        };

        private static readonly LegacyMap[] LegacyMaps = {
                                                             new LegacyMap(DXGIFormat.BC1_UNorm, ConversionFlags.None, PixelFormat.DXT1), // D3DFMT_DXT1
                                                             new LegacyMap(DXGIFormat.BC2_UNorm, ConversionFlags.None, PixelFormat.DXT3), // D3DFMT_DXT3
                                                             new LegacyMap(DXGIFormat.BC3_UNorm, ConversionFlags.None, PixelFormat.DXT5), // D3DFMT_DXT5

                                                             new LegacyMap(DXGIFormat.BC2_UNorm, ConversionFlags.None, PixelFormat.DXT2), // D3DFMT_DXT2 (ignore premultiply)
                                                             new LegacyMap(DXGIFormat.BC3_UNorm, ConversionFlags.None, PixelFormat.DXT4), // D3DFMT_DXT4 (ignore premultiply)

                                                             new LegacyMap(DXGIFormat.BC4_UNorm, ConversionFlags.None, PixelFormat.BC4_UNorm),
                                                             new LegacyMap(DXGIFormat.BC4_SNorm, ConversionFlags.None, PixelFormat.BC4_SNorm),
                                                             new LegacyMap(DXGIFormat.BC5_UNorm, ConversionFlags.None, PixelFormat.BC5_UNorm),
                                                             new LegacyMap(DXGIFormat.BC5_SNorm, ConversionFlags.None, PixelFormat.BC5_SNorm),

                                                             new LegacyMap(DXGIFormat.BC4_UNorm, ConversionFlags.None, new PixelFormat(PixelFormatFlags.FourCC, new FourCC('A', 'T', 'I', '1'), 0, 0, 0, 0, 0)),
                                                             new LegacyMap(DXGIFormat.BC5_UNorm, ConversionFlags.None, new PixelFormat(PixelFormatFlags.FourCC, new FourCC('A', 'T', 'I', '2'), 0, 0, 0, 0, 0)),

                                                             new LegacyMap(DXGIFormat.R8G8_B8G8_UNorm, ConversionFlags.None, PixelFormat.R8G8_B8G8), // D3DFMT_R8G8_B8G8
                                                             new LegacyMap(DXGIFormat.G8R8_G8B8_UNorm, ConversionFlags.None, PixelFormat.G8R8_G8B8), // D3DFMT_G8R8_G8B8

                                                             new LegacyMap(DXGIFormat.B8G8R8A8_UNorm, ConversionFlags.None, PixelFormat.A8R8G8B8), // D3DFMT_A8R8G8B8 (uses DXGI 1.1 format)
                                                             new LegacyMap(DXGIFormat.B8G8R8X8_UNorm, ConversionFlags.None, PixelFormat.X8R8G8B8), // D3DFMT_X8R8G8B8 (uses DXGI 1.1 format)
                                                             new LegacyMap(DXGIFormat.R8G8B8A8_UNorm, ConversionFlags.None, PixelFormat.A8B8G8R8), // D3DFMT_A8B8G8R8
                                                             new LegacyMap(DXGIFormat.R8G8B8A8_UNorm, ConversionFlags.NoAlpha, PixelFormat.X8B8G8R8), // D3DFMT_X8B8G8R8
                                                             new LegacyMap(DXGIFormat.R16G16_UNorm, ConversionFlags.None, PixelFormat.G16R16), // D3DFMT_G16R16

                                                             new LegacyMap(DXGIFormat.R10G10B10A2_UNorm, ConversionFlags.Swizzle, new PixelFormat(PixelFormatFlags.Rgb, 0, 32, 0x000003ff, 0x000ffc00, 0x3ff00000, 0xc0000000)),
                                                             // D3DFMT_A2R10G10B10 (D3DX reversal issue workaround)
                                                             new LegacyMap(DXGIFormat.R10G10B10A2_UNorm, ConversionFlags.None, new PixelFormat(PixelFormatFlags.Rgb, 0, 32, 0x3ff00000, 0x000ffc00, 0x000003ff, 0xc0000000)),
                                                             // D3DFMT_A2B10G10R10 (D3DX reversal issue workaround)

                                                             new LegacyMap(DXGIFormat.R8G8B8A8_UNorm, ConversionFlags.Expand
                                                                                                       | ConversionFlags.NoAlpha
                                                                                                       | ConversionFlags.Format888, PixelFormat.R8G8B8), // D3DFMT_R8G8B8

                                                             new LegacyMap(DXGIFormat.B5G6R5_UNorm, ConversionFlags.Format565, PixelFormat.R5G6B5), // D3DFMT_R5G6B5
                                                             new LegacyMap(DXGIFormat.B5G5R5A1_UNorm, ConversionFlags.Format5551, PixelFormat.A1R5G5B5), // D3DFMT_A1R5G5B5
                                                             new LegacyMap(DXGIFormat.B5G5R5A1_UNorm, ConversionFlags.Format5551
                                                                                                       | ConversionFlags.NoAlpha, new PixelFormat(PixelFormatFlags.Rgb, 0, 16, 0x7c00, 0x03e0, 0x001f, 0x0000)), // D3DFMT_X1R5G5B5
 
                                                             new LegacyMap(DXGIFormat.R8G8B8A8_UNorm, ConversionFlags.Expand
                                                                                                       | ConversionFlags.Format8332, new PixelFormat(PixelFormatFlags.Rgb, 0, 16, 0x00e0, 0x001c, 0x0003, 0xff00)),
                                                             // D3DFMT_A8R3G3B2
                                                             new LegacyMap(DXGIFormat.B5G6R5_UNorm, ConversionFlags.Expand
                                                                                                     | ConversionFlags.Format332, new PixelFormat(PixelFormatFlags.Rgb, 0, 8, 0xe0, 0x1c, 0x03, 0x00)), // D3DFMT_R3G3B2

                                                             new LegacyMap(DXGIFormat.R8_UNorm, ConversionFlags.None, PixelFormat.L8), // D3DFMT_L8
                                                             new LegacyMap(DXGIFormat.R16_UNorm, ConversionFlags.None, PixelFormat.L16), // D3DFMT_L16
                                                             new LegacyMap(DXGIFormat.R8G8_UNorm, ConversionFlags.None, PixelFormat.A8L8), // D3DFMT_A8L8

                                                             new LegacyMap(DXGIFormat.A8_UNorm, ConversionFlags.None, PixelFormat.A8), // D3DFMT_A8

                                                             new LegacyMap(DXGIFormat.R16G16B16A16_UNorm, ConversionFlags.None, new PixelFormat(PixelFormatFlags.FourCC, 36, 0, 0, 0, 0, 0)), // D3DFMT_A16B16G16R16
                                                             new LegacyMap(DXGIFormat.R16G16B16A16_SNorm, ConversionFlags.None, new PixelFormat(PixelFormatFlags.FourCC, 110, 0, 0, 0, 0, 0)), // D3DFMT_Q16W16V16U16
                                                             new LegacyMap(DXGIFormat.R16_Float, ConversionFlags.None, new PixelFormat(PixelFormatFlags.FourCC, 111, 0, 0, 0, 0, 0)), // D3DFMT_R16F
                                                             new LegacyMap(DXGIFormat.R16G16_Float, ConversionFlags.None, new PixelFormat(PixelFormatFlags.FourCC, 112, 0, 0, 0, 0, 0)), // D3DFMT_G16R16F
                                                             new LegacyMap(DXGIFormat.R16G16B16A16_Float, ConversionFlags.None, new PixelFormat(PixelFormatFlags.FourCC, 113, 0, 0, 0, 0, 0)), // D3DFMT_A16B16G16R16F
                                                             new LegacyMap(DXGIFormat.R32_Float, ConversionFlags.None, new PixelFormat(PixelFormatFlags.FourCC, 114, 0, 0, 0, 0, 0)), // D3DFMT_R32F
                                                             new LegacyMap(DXGIFormat.R32G32_Float, ConversionFlags.None, new PixelFormat(PixelFormatFlags.FourCC, 115, 0, 0, 0, 0, 0)), // D3DFMT_G32R32F
                                                             new LegacyMap(DXGIFormat.R32G32B32A32_Float, ConversionFlags.None, new PixelFormat(PixelFormatFlags.FourCC, 116, 0, 0, 0, 0, 0)), // D3DFMT_A32B32G32R32F

                                                             new LegacyMap(DXGIFormat.R32_Float, ConversionFlags.None, new PixelFormat(PixelFormatFlags.Rgb, 0, 32, 0xffffffff, 0x00000000, 0x00000000, 0x00000000)),
                                                             // D3DFMT_R32F (D3DX uses FourCC 114 instead)

                                                             new LegacyMap(DXGIFormat.R8G8B8A8_UNorm, ConversionFlags.Expand
                                                                                                       | ConversionFlags.Pal8
                                                                                                       | ConversionFlags.FormatA8P8, new PixelFormat(PixelFormatFlags.Pal8, 0, 16, 0, 0, 0, 0)), // D3DFMT_A8P8
                                                             new LegacyMap(DXGIFormat.R8G8B8A8_UNorm, ConversionFlags.Expand
                                                                                                       | ConversionFlags.Pal8, new PixelFormat(PixelFormatFlags.Pal8, 0, 8, 0, 0, 0, 0)), // D3DFMT_P8

                                                            new LegacyMap( DXGIFormat.B4G4R4A4_UNorm,     ConversionFlags.Format4444, PixelFormat.A4R4G4B4 ), // D3DFMT_A4R4G4B4 (uses DXGI 1.2 format)
                                                            new LegacyMap( DXGIFormat.B4G4R4A4_UNorm,     ConversionFlags.NoAlpha
                                                                                              | ConversionFlags.Format4444, new PixelFormat(PixelFormatFlags.Rgb,       0, 16, 0x0f00,     0x00f0,     0x000f,     0x0000     ) ), // D3DFMT_X4R4G4B4 (uses DXGI 1.2 format)
                                                            new LegacyMap( DXGIFormat.B4G4R4A4_UNorm,     ConversionFlags.Expand
                                                                                                        | ConversionFlags.Format44, new PixelFormat(PixelFormatFlags.Luminance, 0,  8, 0x0f,       0x00,       0x00,       0xf0       ) ), // D3DFMT_A4L4 (uses DXGI 1.2 format)
                                                             // !DXGI_1_2_FORMATS
                                                             new LegacyMap(DXGIFormat.R8G8B8A8_UNorm, ConversionFlags.Expand
                                                                                                       | ConversionFlags.Format4444, PixelFormat.A4R4G4B4), // D3DFMT_A4R4G4B4
                                                             new LegacyMap(DXGIFormat.R8G8B8A8_UNorm, ConversionFlags.Expand
                                                                                                       | ConversionFlags.NoAlpha
                                                                                                       | ConversionFlags.Format4444, new PixelFormat(PixelFormatFlags.Rgb, 0, 16, 0x0f00, 0x00f0, 0x000f, 0x0000)),
                                                             // D3DFMT_X4R4G4B4
                                                             new LegacyMap(DXGIFormat.R8G8B8A8_UNorm, ConversionFlags.Expand
                                                                                                       | ConversionFlags.Format44, new PixelFormat(PixelFormatFlags.Luminance, 0, 8, 0x0f, 0x00, 0x00, 0xf0)), // D3DFMT_A4L4
                                                             };


        // Note that many common DDS reader/writers (including D3DX) swap the
        // RED/BLUE masks for 10:10:10:2 formats. We assume
        // below that the 'backwards' header mask is being used since it is most
        // likely written by D3DX. The more robust solution is to use the 'DX10'
        // header extension and specify the DXGIFormat.R10G10B10A2_UNorm format directly

        // We do not support the following legacy Direct3D 9 formats:
        //      BumpDuDv D3DFMT_V8U8, D3DFMT_Q8W8V8U8, D3DFMT_V16U16, D3DFMT_A2W10V10U10
        //      BumpLuminance D3DFMT_L6V5U5, D3DFMT_X8L8V8U8
        //      FourCC "UYVY" D3DFMT_UYVY
        //      FourCC "YUY2" D3DFMT_YUY2
        //      FourCC 117 D3DFMT_CxV8U8
        //      ZBuffer D3DFMT_D16_LOCKABLE
        //      FourCC 82 D3DFMT_D32F_LOCKABLE
        private static DXGIFormat GetDXGIFormat(ref PixelFormat pixelFormat, DdsFlags flags, out ConversionFlags conversionFlags)
        {
            conversionFlags = ConversionFlags.None;

            int index;
            for (index = 0; index < LegacyMaps.Length; ++index)
            {
                var entry = LegacyMaps[index];

                if ((pixelFormat.Flags & entry.PixelFormat.Flags) != 0)
                {
                    if ((entry.PixelFormat.Flags & PixelFormatFlags.FourCC) != 0)
                    {
                        if (pixelFormat.FourCC == entry.PixelFormat.FourCC)
                            break;
                    }
                    else if ((entry.PixelFormat.Flags & PixelFormatFlags.Pal8) != 0)
                    {
                        if (pixelFormat.RGBBitCount == entry.PixelFormat.RGBBitCount)
                            break;
                    }
                    else if (pixelFormat.RGBBitCount == entry.PixelFormat.RGBBitCount)
                    {
                        // RGB, RGBA, ALPHA, LUMINANCE
                        if (pixelFormat.RBitMask == entry.PixelFormat.RBitMask
                            && pixelFormat.GBitMask == entry.PixelFormat.GBitMask
                            && pixelFormat.BBitMask == entry.PixelFormat.BBitMask
                            && pixelFormat.ABitMask == entry.PixelFormat.ABitMask)
                            break;
                    }
                }
            }

            if (index >= LegacyMaps.Length)
                return DXGIFormat.Unknown;

            conversionFlags = LegacyMaps[index].ConversionFlags;
            var format = LegacyMaps[index].DXGIFormat;

            if ((conversionFlags & ConversionFlags.Expand) != 0 && (flags & DdsFlags.NoLegacyExpansion) != 0)
                return DXGIFormat.Unknown;

            if ((format == DXGIFormat.R10G10B10A2_UNorm) && (flags & DdsFlags.NoR10B10G10A2Fixup) != 0)
            {
                conversionFlags ^= ConversionFlags.Swizzle;
            }

            return format;
        }


        /// <summary>
        /// Decodes DDS header including optional DX10 extended header
        /// </summary>
        /// <param name="headerPtr">Pointer to the DDS header.</param>
        /// <param name="size">Size of the DDS content.</param>
        /// <param name="flags">Flags used for decoding the DDS header.</param>
        /// <param name="description">Output texture description.</param>
        /// <param name="convFlags">Output conversion flags.</param>
        /// <exception cref="ArgumentException">If the argument headerPtr is null</exception>
        /// <exception cref="InvalidOperationException">If the DDS header contains invalid data.</exception>
        /// <returns>True if the decoding is successful, false if this is not a DDS header.</returns>
        private static unsafe bool DecodeDDSHeader(IntPtr headerPtr, long size, DdsFlags flags, out ImageDescription description, out DXGIFormat dxgiFormat, out ConversionFlags convFlags)
        {
            description = new ImageDescription();
            convFlags = ConversionFlags.None;
            dxgiFormat = DXGIFormat.Unknown;

            if (headerPtr == IntPtr.Zero)
                throw new ArgumentException("Pointer to DDS header cannot be null", "headerPtr");

            if (size < (Utilities.SizeOf<Header>() + sizeof (uint)))
                return false;

            // DDS files always start with the same magic number ("DDS ")
            if (*(uint*) (headerPtr) != MagicHeader)
                return false;

            var header = *(Header*) ((byte*) headerPtr + sizeof (int));

            // Verify header to validate DDS file
            if (header.Size != Utilities.SizeOf<Header>() || header.PixelFormat.Size != Utilities.SizeOf<PixelFormat>())
                return false;

            // Setup MipLevels
            description.MipLevels = (uint)header.MipMapCount;
            if (description.MipLevels == 0)
                description.MipLevels = 1;

            // Check for DX10 extension
            if ((header.PixelFormat.Flags & PixelFormatFlags.FourCC) != 0 && (new FourCC('D', 'X', '1', '0') == header.PixelFormat.FourCC))
            {
                // Buffer must be big enough for both headers and magic value
                if (size < (Utilities.SizeOf<Header>() + sizeof (uint) + Utilities.SizeOf<HeaderDXT10>()))
                    return false;

                var headerDX10 = *(HeaderDXT10*) ((byte*) headerPtr + sizeof (int) + Utilities.SizeOf<Header>());
                convFlags |= ConversionFlags.DX10;

                description.ArraySize = (uint)headerDX10.ArraySize;
                if (description.ArraySize == 0)
                    throw new InvalidOperationException("Unexpected ArraySize == 0 from DDS HeaderDX10 ");

                if (!DXGIFormatHelper.IsValid(headerDX10.DXGIFormat))
                    throw new InvalidOperationException($"Invalid DXGIFormat from DDS HeaderDX10 {headerDX10.DXGIFormat}");

                switch (headerDX10.ResourceDimension)
                {
                    case ResourceDimension.Texture1D:

                        // D3DX writes 1D textures with a fixed Height of 1
                        if ((header.Flags & HeaderFlags.Height) != 0 && header.Height != 1)
                            throw new InvalidOperationException("Unexpected Height != 1 from DDS HeaderDX10 ");

                        description.Width = (uint)header.Width;
                        description.Height = 1;
                        description.Depth = 1;
                        description.Dimension = TextureDimension.Texture1D;
                        break;

                    case ResourceDimension.Texture2D:
                        if ((headerDX10.MiscFlags & ResourceOptionFlags.TextureCube) != 0)
                        {
                            description.ArraySize *= 6;
                            description.Dimension = TextureDimension.TextureCube;
                        }
                        else
                        {
                            description.Dimension = TextureDimension.Texture2D;
                        }

                        description.Width = (uint)header.Width;
                        description.Height = (uint)header.Height;
                        description.Depth = 1;
                        break;

                    case ResourceDimension.Texture3D:
                        if ((header.Flags & HeaderFlags.Volume) == 0)
                            throw new InvalidOperationException("Texture3D missing HeaderFlags.Volume from DDS HeaderDX10");

                        if (description.ArraySize > 1)
                            throw new InvalidOperationException("Unexpected ArraySize > 1 for Texture3D from DDS HeaderDX10");

                        description.Width = (uint)header.Width;
                        description.Height = (uint)header.Height;
                        description.Depth = (uint)header.Depth;
                        description.Dimension = TextureDimension.Texture3D;
                        break;

                    default:
                        throw new InvalidOperationException($"Unexpected dimension [{headerDX10.ResourceDimension}] from DDS HeaderDX10");
                }
            }
            else
            {
                description.ArraySize = 1;

                if ((header.Flags & HeaderFlags.Volume) != 0)
                {
                    description.Width = (uint)header.Width;
                    description.Height = (uint)header.Height;
                    description.Depth = (uint)header.Depth;
                    description.Dimension = TextureDimension.Texture3D;
                }
                else
                {
                    if ((header.CubemapFlags & CubemapFlags.CubeMap) != 0)
                    {
                        // We require all six faces to be defined
                        if ((header.CubemapFlags & CubemapFlags.AllFaces) != CubemapFlags.AllFaces)
                            throw new InvalidOperationException("Unexpected CubeMap, expecting all faces from DDS Header");

                        description.ArraySize = 6;
                        description.Dimension = TextureDimension.TextureCube;
                    }
                    else
                    {
                        description.Dimension = TextureDimension.Texture2D;
                    }

                    description.Width = (uint)header.Width;
                    description.Height = (uint)header.Height;
                    description.Depth = 1;
                    // Note there's no way for a legacy Direct3D 9 DDS to express a '1D' texture
                }

                dxgiFormat = GetDXGIFormat(ref header.PixelFormat, flags, out convFlags);

                if (dxgiFormat == DXGIFormat.Unknown)
                    throw new InvalidOperationException("Unsupported PixelFormat from DDS Header");
            }

            // Special flag for handling BGR DXGI 1.1 formats
            if ((flags & DdsFlags.ForceRgb) != 0)
            {
                switch (dxgiFormat)
                {
                    case DXGIFormat.B8G8R8A8_UNorm:
                        dxgiFormat = DXGIFormat.R8G8B8A8_UNorm;
                        convFlags |= ConversionFlags.Swizzle;
                        break;

                    case DXGIFormat.B8G8R8X8_UNorm:
                        dxgiFormat = DXGIFormat.R8G8B8A8_UNorm;
                        convFlags |= ConversionFlags.Swizzle | ConversionFlags.NoAlpha;
                        break;

                    case DXGIFormat.B8G8R8A8_Typeless:
                        dxgiFormat = DXGIFormat.R8G8B8A8_Typeless;
                        convFlags |= ConversionFlags.Swizzle;
                        break;

                    case DXGIFormat.B8G8R8A8_UNorm_SRgb:
                        dxgiFormat = DXGIFormat.R8G8B8A8_UNorm_SRgb;
                        convFlags |= ConversionFlags.Swizzle;
                        break;

                    case DXGIFormat.B8G8R8X8_Typeless:
                        dxgiFormat = DXGIFormat.R8G8B8A8_Typeless;
                        convFlags |= ConversionFlags.Swizzle | ConversionFlags.NoAlpha;
                        break;

                    case DXGIFormat.B8G8R8X8_UNorm_SRgb:
                        dxgiFormat = DXGIFormat.R8G8B8A8_UNorm_SRgb;
                        convFlags |= ConversionFlags.Swizzle | ConversionFlags.NoAlpha;
                        break;
                }
            }

            // Pass DDSFlags copy memory to the conversion flags
            if ((flags & DdsFlags.CopyMemory) != 0)
                convFlags |= ConversionFlags.CopyMemory;

            // Special flag for handling 16bpp formats
            if ((flags & DdsFlags.No16Bpp) != 0)
            {
                switch (dxgiFormat)
                {
                    case DXGIFormat.B5G6R5_UNorm:
                    case DXGIFormat.B5G5R5A1_UNorm:
                    case DXGIFormat.B4G4R4A4_UNorm:
                        dxgiFormat = DXGIFormat.R8G8B8A8_UNorm;
                        convFlags |= ConversionFlags.Expand;
                        if (dxgiFormat == DXGIFormat.B5G6R5_UNorm)
                            convFlags |= ConversionFlags.NoAlpha;
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// Encodes DDS file header (magic value, header, optional DX10 extended header)
        /// </summary>
        /// <param name="flags">Flags used for decoding the DDS header.</param>
        /// <param name="description">Output texture description.</param>
        /// <param name="pDestination">Pointer to the DDS output header. Can be set to IntPtr.Zero to calculated the required bytes.</param>
        /// <param name="maxsize">The maximum size of the destination buffer.</param>
        /// <param name="required">Output the number of bytes required to write the DDS header.</param>
        /// <exception cref="ArgumentException">If the argument headerPtr is null</exception>
        /// <exception cref="InvalidOperationException">If the DDS header contains invalid data.</exception>
        /// <returns>True if the decoding is successful, false if this is not a DDS header.</returns>
        private static unsafe void EncodeDDSHeader( ImageDescription description, DdsFlags flags, IntPtr pDestination, int maxsize, out int required )
        {
            if (description.ArraySize > 1)
            {
                if ((description.ArraySize != 6) || (description.Dimension != TextureDimension.Texture2D) || (description.Dimension != TextureDimension.TextureCube))
                {
                    flags |= DdsFlags.ForceDX10Ext;
                }
            }

            var dxgiFormat = FormatConverter.VulkanToDXGI(description.Format);

            var ddpf = default(PixelFormat);
            if ((flags & DdsFlags.ForceDX10Ext) == 0)
            {
                switch (dxgiFormat)
                {
                    case DXGIFormat.R8G8B8A8_UNorm:
                        ddpf = PixelFormat.A8B8G8R8;
                        break;
                    case DXGIFormat.R16G16_UNorm:
                        ddpf = PixelFormat.G16R16;
                        break;
                    case DXGIFormat.R8G8_UNorm:
                        ddpf = PixelFormat.A8L8;
                        break;
                    case DXGIFormat.R16_UNorm:
                        ddpf = PixelFormat.L16;
                        break;
                    case DXGIFormat.R8_UNorm:
                        ddpf = PixelFormat.L8;
                        break;
                    case DXGIFormat.A8_UNorm:
                        ddpf = PixelFormat.A8;
                        break;
                    case DXGIFormat.R8G8_B8G8_UNorm:
                        ddpf = PixelFormat.R8G8_B8G8;
                        break;
                    case DXGIFormat.G8R8_G8B8_UNorm:
                        ddpf = PixelFormat.G8R8_G8B8;
                        break;
                    case DXGIFormat.BC1_UNorm:
                        ddpf = PixelFormat.DXT1;
                        break;
                    case DXGIFormat.BC2_UNorm:
                        ddpf = PixelFormat.DXT3;
                        break;
                    case DXGIFormat.BC3_UNorm:
                        ddpf = PixelFormat.DXT5;
                        break;
                    case DXGIFormat.BC4_UNorm:
                        ddpf = PixelFormat.BC4_UNorm;
                        break;
                    case DXGIFormat.BC4_SNorm:
                        ddpf = PixelFormat.BC4_SNorm;
                        break;
                    case DXGIFormat.BC5_UNorm:
                        ddpf = PixelFormat.BC5_UNorm;
                        break;
                    case DXGIFormat.BC5_SNorm:
                        ddpf = PixelFormat.BC5_SNorm;
                        break;
                    case DXGIFormat.B5G6R5_UNorm:
                        ddpf = PixelFormat.R5G6B5;
                        break;
                    case DXGIFormat.B5G5R5A1_UNorm:
                        ddpf = PixelFormat.A1R5G5B5;
                        break;
                    case DXGIFormat.B8G8R8A8_UNorm:
                        ddpf = PixelFormat.A8R8G8B8;
                        break; // DXGI 1.1
                    case DXGIFormat.B8G8R8X8_UNorm:
                        ddpf = PixelFormat.X8R8G8B8;
                        break; // DXGI 1.1
                    case DXGIFormat.B4G4R4A4_UNorm:
                        ddpf = PixelFormat.A4R4G4B4;
                        break;
                    // Legacy D3DX formats using D3DFMT enum value as FourCC
                    case DXGIFormat.R32G32B32A32_Float:
                        ddpf.Size = Utilities.SizeOf<PixelFormat>();
                        ddpf.Flags = PixelFormatFlags.FourCC;
                        ddpf.FourCC = 116; // D3DFMT_A32B32G32R32F
                        break;
                    case DXGIFormat.R16G16B16A16_Float:
                        ddpf.Size = Utilities.SizeOf<PixelFormat>();
                        ddpf.Flags = PixelFormatFlags.FourCC;
                        ddpf.FourCC = 113; // D3DFMT_A16B16G16R16F
                        break;
                    case DXGIFormat.R16G16B16A16_UNorm:
                        ddpf.Size = Utilities.SizeOf<PixelFormat>();
                        ddpf.Flags = PixelFormatFlags.FourCC;
                        ddpf.FourCC = 36; // D3DFMT_A16B16G16R16
                        break;
                    case DXGIFormat.R16G16B16A16_SNorm:
                        ddpf.Size = Utilities.SizeOf<PixelFormat>();
                        ddpf.Flags = PixelFormatFlags.FourCC;
                        ddpf.FourCC = 110; // D3DFMT_Q16W16V16U16
                        break;
                    case DXGIFormat.R32G32_Float:
                        ddpf.Size = Utilities.SizeOf<PixelFormat>();
                        ddpf.Flags = PixelFormatFlags.FourCC;
                        ddpf.FourCC = 115; // D3DFMT_G32R32F
                        break;
                    case DXGIFormat.R16G16_Float:
                        ddpf.Size = Utilities.SizeOf<PixelFormat>();
                        ddpf.Flags = PixelFormatFlags.FourCC;
                        ddpf.FourCC = 112; // D3DFMT_G16R16F
                        break;
                    case DXGIFormat.R32_Float:
                        ddpf.Size = Utilities.SizeOf<PixelFormat>();
                        ddpf.Flags = PixelFormatFlags.FourCC;
                        ddpf.FourCC = 114; // D3DFMT_R32F
                        break;
                    case DXGIFormat.R16_Float:
                        ddpf.Size = Utilities.SizeOf<PixelFormat>();
                        ddpf.Flags = PixelFormatFlags.FourCC;
                        ddpf.FourCC = 111; // D3DFMT_R16F
                        break;
                }
            }

            required = sizeof (int) + Utilities.SizeOf<Header>();

            if (ddpf.Size == 0)
                required += Utilities.SizeOf<HeaderDXT10>();

            if (pDestination == IntPtr.Zero)
                return;

            if (maxsize < required)
                throw new ArgumentException("Not enough size for destination buffer", nameof(maxsize));

            *(uint*)(pDestination) = MagicHeader;

            var header = (Header*)((byte*)(pDestination) + sizeof (int));
            var headerPtr = (IntPtr)header;
            Utilities.ClearMemory(ref headerPtr, 0, Utilities.SizeOf<Header>());
            header->Size = Utilities.SizeOf<Header>();
            header->Flags = HeaderFlags.Texture;
            header->SurfaceFlags = SurfaceFlags.Texture;

            if (description.MipLevels > 0)
            {
                header->Flags |= HeaderFlags.Mipmap;
                header->MipMapCount = (int)description.MipLevels;

                if (header->MipMapCount > 1)
                    header->SurfaceFlags |= SurfaceFlags.Mipmap;
            }

            switch (description.Dimension)
            {
                case TextureDimension.Texture1D:
                    header->Height = (int)description.Height;
                    header->Width = header->Depth = 1;
                    break;

                case TextureDimension.Texture2D:
                case TextureDimension.TextureCube:
                    header->Height = (int)description.Height;
                    header->Width = (int)description.Width;
                    header->Depth = 1;

                    if (description.Dimension == TextureDimension.TextureCube)
                    {
                        header->SurfaceFlags |= SurfaceFlags.Cubemap;
                        header->CubemapFlags |= CubemapFlags.AllFaces;
                    }
                    break;

                case TextureDimension.Texture3D:

                    header->Flags |= HeaderFlags.Volume;
                    header->CubemapFlags |= CubemapFlags.Volume;
                    header->Height = (int)description.Height;
                    header->Width = (int)description.Width;
                    header->Depth = (int)description.Depth;
                    break;
            }

            int rowPitch, slicePitch;
            int newWidth;
            int newHeight;
            Image.ComputePitch(description.Format, (int)description.Width, (int)description.Height, out rowPitch, out slicePitch, out newWidth, out newHeight);

            if (description.Format.IsCompressed())
            {
                header->Flags |= HeaderFlags.LinearSize;
                header->PitchOrLinearSize = slicePitch;
            }
            else
            {
                header->Flags |= HeaderFlags.Pitch;
                header->PitchOrLinearSize = rowPitch;
            }

            if (ddpf.Size == 0)
            {
                header->PixelFormat = PixelFormat.DX10;

                var ext = (HeaderDXT10*)((byte*)(header) + Utilities.SizeOf<Header>());
                var extPtr = (IntPtr)ext;
                Utilities.ClearMemory(ref extPtr, 0, Utilities.SizeOf<HeaderDXT10>());

                ext->DXGIFormat = dxgiFormat;
                switch (description.Dimension)
                {
                    case TextureDimension.Texture1D:
                        ext->ResourceDimension = ResourceDimension.Texture1D;
                        break;
                    case TextureDimension.Texture2D:
                    case TextureDimension.TextureCube:
                        ext->ResourceDimension = ResourceDimension.Texture2D;
                        break;
                    case TextureDimension.Texture3D:
                        ext->ResourceDimension = ResourceDimension.Texture3D;
                        break;

                }

                if (description.Dimension == TextureDimension.TextureCube)
                {
                    ext->MiscFlags |= ResourceOptionFlags.TextureCube;
                    ext->ArraySize = (int)(description.ArraySize / 6);
                }
                else
                {
                    ext->ArraySize = (int)description.ArraySize;
                }
            }
            else
            {
                header->PixelFormat = ddpf;
            }
        }

        enum TEXP_LEGACY_FORMAT
        {
            UNKNOWN = 0,
            R8G8B8,
            R3G3B2,
            A8R3G3B2,
            P8,
            A8P8,
            A4L4,
            B4G4R4A4,
        };

        private static TEXP_LEGACY_FORMAT FindLegacyFormat(ConversionFlags flags)
        {
            var lformat = TEXP_LEGACY_FORMAT.UNKNOWN;

            if ((flags & ConversionFlags.Pal8) != 0)
            {
                lformat = (flags & ConversionFlags.FormatA8P8) != 0 ? TEXP_LEGACY_FORMAT.A8P8 : TEXP_LEGACY_FORMAT.P8;
            }
            else if ((flags & ConversionFlags.Format888) != 0)
                lformat = TEXP_LEGACY_FORMAT.R8G8B8;
            else if ((flags & ConversionFlags.Format332) != 0)
                lformat = TEXP_LEGACY_FORMAT.R3G3B2;
            else if ((flags & ConversionFlags.Format8332) != 0)
                lformat = TEXP_LEGACY_FORMAT.A8R3G3B2;
            else if ((flags & ConversionFlags.Format44) != 0)
                lformat = TEXP_LEGACY_FORMAT.A4L4;
            else if ((flags & ConversionFlags.Format4444) != 0)
                lformat = TEXP_LEGACY_FORMAT.B4G4R4A4;
            return lformat;
        }

        /// <summary>
        /// Converts an image row with optional clearing of alpha value to 1.0
        /// </summary>
        /// <param name="pDestination"></param>
        /// <param name="outSize"></param>
        /// <param name="outFormat"></param>
        /// <param name="pSource"></param>
        /// <param name="inSize"></param>
        /// <param name="inFormat"></param>
        /// <param name="pal8"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        static unsafe bool LegacyExpandScanline( IntPtr pDestination, int outSize, Format outFormat, 
                                            IntPtr pSource, int inSize, TEXP_LEGACY_FORMAT inFormat,
                                            int* pal8, ImageHelper.ScanlineFlags flags )
        {
            switch (inFormat)
            {
                case TEXP_LEGACY_FORMAT.R8G8B8:
                    if (outFormat != Format.R8G8B8A8_UNORM)
                        return false;

                    // D3DFMT_R8G8B8 -> DXGIFormat.R8G8B8A8_UNorm
                    {
                        var sPtr = (byte*) (pSource);
                        var dPtr = (int*) (pDestination);

                        for (int ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); icount += 3, ocount += 4)
                        {
                            // 24bpp Direct3D 9 files are actually BGR, so need to swizzle as well
                            int t1 = (*(sPtr) << 16);
                            int t2 = (*(sPtr + 1) << 8);
                            int t3 = *(sPtr + 2);

                            *(dPtr++) = (int) (t1 | t2 | t3 | 0xff000000);
                            sPtr += 3;
                        }
                    }
                    return true;

                case TEXP_LEGACY_FORMAT.R3G3B2:
                    switch (outFormat)
                    {
                        case Format.R8G8B8A8_UNORM:
                            // D3DFMT_R3G3B2 -> DXGIFormat.R8G8B8A8_UNorm
                            {
                                var sPtr = (byte*) (pSource);
                                var dPtr = (int*) (pDestination);

                                for (int ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); ++icount, ocount += 4)
                                {
                                    byte t = *(sPtr++);

                                    int t1 = (t & 0xe0) | ((t & 0xe0) >> 3) | ((t & 0xc0) >> 6);
                                    int t2 = ((t & 0x1c) << 11) | ((t & 0x1c) << 8) | ((t & 0x18) << 5);
                                    int t3 = ((t & 0x03) << 22) | ((t & 0x03) << 20) | ((t & 0x03) << 18) | ((t & 0x03) << 16);

                                    *(dPtr++) = (int) (t1 | t2 | t3 | 0xff000000);
                                }
                            }
                            return true;

                        case Format.B5G6R5_UNORM_PACK16:
                            // D3DFMT_R3G3B2 -> DXGIFormat.B5G6R5_UNorm
                            {
                                var sPtr = (byte*) (pSource);
                                var dPtr = (short*) (pDestination);

                                for (int ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); ++icount, ocount += 2)
                                {
                                    byte t = *(sPtr++);

                                    var t1 = (short) (((t & 0xe0) << 8) | ((t & 0xc0) << 5));
                                    var t2 = (short) (((t & 0x1c) << 6) | ((t & 0x1c) << 3));
                                    var t3 = (short) (((t & 0x03) << 3) | ((t & 0x03) << 1) | ((t & 0x02) >> 1));

                                    *(dPtr++) = (short) (t1 | t2 | t3);
                                }
                            }
                            return true;
                    }
                    break;

                case TEXP_LEGACY_FORMAT.A8R3G3B2:
                    if (outFormat != Format.R8G8B8A8_UNORM)
                        return false;

                    // D3DFMT_A8R3G3B2 -> DXGIFormat.R8G8B8A8_UNorm
                    {
                        var sPtr = (short*) (pSource);
                        var dPtr = (int*) (pDestination);

                        for (int ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); icount += 2, ocount += 4)
                        {
                            short t = *(sPtr++);

                            int t1 = (t & 0x00e0) | ((t & 0x00e0) >> 3) | ((t & 0x00c0) >> 6);
                            int t2 = ((t & 0x001c) << 11) | ((t & 0x001c) << 8) | ((t & 0x0018) << 5);
                            int t3 = ((t & 0x0003) << 22) | ((t & 0x0003) << 20) | ((t & 0x0003) << 18) | ((t & 0x0003) << 16);
                            uint ta = ((flags & ImageHelper.ScanlineFlags.SetAlpha) != 0 ? 0xff000000 : (uint) ((t & 0xff00) << 16));

                            *(dPtr++) = (int) (t1 | t2 | t3 | ta);
                        }
                    }
                    return true;

                case TEXP_LEGACY_FORMAT.P8:
                    if ((outFormat != Format.R8G8B8A8_UNORM) || pal8 == null)
                        return false;

                    // D3DFMT_P8 -> DXGIFormat.R8G8B8A8_UNorm
                    {
                        byte* sPtr = (byte*) (pSource);
                        int* dPtr = (int*) (pDestination);

                        for (int ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); ++icount, ocount += 4)
                        {
                            byte t = *(sPtr++);

                            *(dPtr++) = pal8[t];
                        }
                    }
                    return true;

                case TEXP_LEGACY_FORMAT.A8P8:
                    if ((outFormat != Format.R8G8B8A8_UNORM) || pal8 == null)
                        return false;

                    // D3DFMT_A8P8 -> DXGIFormat.R8G8B8A8_UNorm
                    {
                        short* sPtr = (short*) (pSource);
                        int* dPtr = (int*) (pDestination);

                        for (int ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); icount += 2, ocount += 4)
                        {
                            short t = *(sPtr++);

                            int t1 = pal8[t & 0xff];
                            uint ta = ((flags & ImageHelper.ScanlineFlags.SetAlpha) != 0 ? 0xff000000 : (uint) ((t & 0xff00) << 16));

                            *(dPtr++) = (int) (t1 | ta);
                        }
                    }
                    return true;

                case TEXP_LEGACY_FORMAT.A4L4:
                    switch (outFormat)
                    {
                case Format.B4G4R4A4_UNORM_PACK16:
                    // D3DFMT_A4L4 -> DXGIFormat.B4G4R4A4_UNorm 
                    {
                        byte * sPtr = (byte*)(pSource);
                        short * dPtr = (short*)(pDestination);

                        for( int ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); ++icount, ocount += 2 )
                        {
                            byte t = *(sPtr++);

                            short t1 = (short)(t & 0x0f);
                            ushort ta = (flags & ImageHelper.ScanlineFlags.SetAlpha ) != 0 ?  (ushort)0xf000 : (ushort)((t & 0xf0) << 8);

                            *(dPtr++) = (short)(t1 | (t1 << 4) | (t1 << 8) | ta);
                        }
                    }
                    return true;
                            // DXGI_1_2_FORMATS

                        case Format.R8G8B8A8_UNORM:
                            // D3DFMT_A4L4 -> DXGIFormat.R8G8B8A8_UNorm
                            {
                                byte* sPtr = (byte*) (pSource);
                                int* dPtr = (int*) (pDestination);

                                for (int ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); ++icount, ocount += 4)
                                {
                                    byte t = *(sPtr++);

                                    int t1 = ((t & 0x0f) << 4) | (t & 0x0f);
                                    uint ta = ((flags & ImageHelper.ScanlineFlags.SetAlpha) != 0 ? 0xff000000 : (uint) (((t & 0xf0) << 24) | ((t & 0xf0) << 20)));

                                    *(dPtr++) = (int) (t1 | (t1 << 8) | (t1 << 16) | ta);
                                }
                            }
                            return true;
                    }
                    break;
                case TEXP_LEGACY_FORMAT.B4G4R4A4:
                    if (outFormat != Format.R8G8B8A8_UNORM)
                        return false;

                    // D3DFMT_A4R4G4B4 -> DXGIFormat.R8G8B8A8_UNorm
                    {
                        short* sPtr = (short*) (pSource);
                        int* dPtr = (int*) (pDestination);

                        for (int ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); icount += 2, ocount += 4)
                        {
                            short t = *(sPtr++);

                            int t1 = ((t & 0x0f00) >> 4) | ((t & 0x0f00) >> 8);
                            int t2 = ((t & 0x00f0) << 8) | ((t & 0x00f0) << 4);
                            int t3 = ((t & 0x000f) << 20) | ((t & 0x000f) << 16);
                            uint ta = ((flags & ImageHelper.ScanlineFlags.SetAlpha) != 0 ? 0xff000000 : (uint) (((t & 0xf000) << 16) | ((t & 0xf000) << 12)));

                            *(dPtr++) = (int) (t1 | t2 | t3 | ta);
                        }
                    }
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Load a DDS file in memory
        /// </summary>
        /// <param name="pSource">Source buffer</param>
        /// <param name="size">Size of the DDS texture.</param>
        /// <param name="makeACopy">Whether or not to make a copy of the DDS</param>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static unsafe IRawBitmap LoadFromMemory(IntPtr pSource, long size, bool makeACopy, GCHandle? handle)
        {
            var flags = makeACopy ? DdsFlags.CopyMemory : DdsFlags.None;

            ConversionFlags convFlags;
            ImageDescription metadata;
            // If the memory pointed is not a DDS memory, return null.
            if (!DecodeDDSHeader(pSource, size, flags, out metadata, out var dxgiFormat, out convFlags))
                throw new ArgumentException("Given file is not a DDS file");

            long offset = sizeof (uint) + Utilities.SizeOf<Header>();
            if ((convFlags & ConversionFlags.DX10) != 0)
                offset += Utilities.SizeOf<HeaderDXT10>();

            var pal8 = (int*) 0;
            if ((convFlags & ConversionFlags.Pal8) != 0)
            {
                pal8 = (int*) ((byte*) (pSource) + offset);
                offset += (256 * sizeof (uint));
            }

            if (size < offset)
                throw new InvalidOperationException();
            var cpFlags = (flags & DdsFlags.LegacyDword) != 0 ? Image.PitchFlags.LegacyDword : Image.PitchFlags.None;
            metadata.Format = FormatConverter.DXGIToVulkan(dxgiFormat);
            var image = CreateImageFromDDS(pSource, offset, (size - offset), metadata, cpFlags, convFlags, pal8);
            return image;
        }

        public static void SaveToStream(Image img, PixelBuffer[] pixelBuffers, int count, ImageDescription description, Stream imageStream)
        {
            SaveToDDSStream(pixelBuffers, count, description, DdsFlags.None, imageStream);
        }

        //-------------------------------------------------------------------------------------
        // Save a DDS to a stream
        //-------------------------------------------------------------------------------------
        public unsafe static void SaveToDDSStream(PixelBuffer[] pixelBuffers, int count, ImageDescription metadata, DdsFlags flags, Stream stream)
        {
            // Determine memory required
            int totalSize;
            int headerSize = 0;
            EncodeDDSHeader(metadata, flags, IntPtr.Zero, 0, out totalSize);
            headerSize = totalSize;

            int maxSlice = 0;

            for (int i = 0; i < pixelBuffers.Length; ++i)
            {
                int slice = pixelBuffers[i].BufferStride;
                totalSize += slice;
                if (slice > maxSlice)
                    maxSlice = slice;
            }

            Debug.Assert(totalSize > 0);

            // Allocate a single temporary buffer to save the headers and each slice.
            var buffer = new byte[Math.Max(maxSlice, headerSize)];

            fixed (void* pbuffer = buffer)
            {
                EncodeDDSHeader(metadata, flags, (IntPtr) pbuffer, headerSize, out var required);
                stream.Write(buffer, 0, headerSize);
            }

            int remaining = totalSize - headerSize;
            Debug.Assert(remaining > 0);

            int index = 0;
            for (int item = 0; item < metadata.ArraySize; ++item)
            {
                uint d = metadata.Depth;

                for (int level = 0; level < metadata.MipLevels; ++level)
                {
                    for (int slice = 0; slice < d; ++slice)
                    {
                        int pixsize = pixelBuffers[index].BufferStride;
                        Utilities.Read(pixelBuffers[index].DataPointer, buffer, 0, pixsize);
                        stream.Write(buffer, 0, pixsize);
                        ++index;
                    }

                    if (d > 1)
                        d >>= 1;
                }
            }
        }

        /// <summary>
        /// Converts or copies image data from pPixels into scratch image data
        /// </summary>
        /// <param name="pDDS"></param>
        /// <param name="offset"></param>
        /// <param name="size"></param>
        /// <param name="metadata"></param>
        /// <param name="cpFlags"></param>
        /// <param name="convFlags"></param>
        /// <param name="pal8"></param>
        /// <param name="handle"></param>
        /// <returns></returns>
        private static unsafe IRawBitmap CreateImageFromDDS(
            IntPtr pDDS, 
            long offset, 
            long size,
            ImageDescription metadata, 
            Image.PitchFlags cpFlags, 
            ConversionFlags convFlags, 
            int* pal8)
        {
            if ((convFlags & ConversionFlags.Expand) != 0)
            {
                if ((convFlags & ConversionFlags.Format888) != 0)
                    cpFlags |= Image.PitchFlags.Bpp24;
                else if ((convFlags & (ConversionFlags.Format565 | ConversionFlags.Format5551 |
                                       ConversionFlags.Format4444 | ConversionFlags.Format8332 |
                                       ConversionFlags.FormatA8P8)) != 0)
                    cpFlags |= Image.PitchFlags.Bpp16;
                else if ((convFlags & (ConversionFlags.Format44 | ConversionFlags.Format332 | ConversionFlags.Pal8)) !=
                         0)
                    cpFlags |= Image.PitchFlags.Bpp8;
            }

            // If source image == dest image and no swizzle/alpha is required, we can return it as-is
            var isCopyNeeded = (convFlags & (ConversionFlags.Expand | ConversionFlags.CopyMemory)) != 0 ||
                               ((cpFlags & Image.PitchFlags.LegacyDword) != 0);
            
            var ddsImage = new DdsImage(metadata);
            var sourcePixelBuffers = ImageHelper.CreatePixelBuffers(metadata, pDDS, offset, cpFlags);

            // Size must be inferior to destination size.
            //Debug.Assert(size <= image.TotalSizeInBytes);

            if (!isCopyNeeded && (convFlags & (ConversionFlags.Swizzle | ConversionFlags.NoAlpha)) == 0)
                return ddsImage;

            var destinationPixelBuffers = ImageHelper.CreatePixelBuffers(metadata, IntPtr.Zero, 0);

            ImageHelper.ScanlineFlags tflags = (convFlags & ConversionFlags.NoAlpha) != 0
                ? ImageHelper.ScanlineFlags.SetAlpha
                : ImageHelper.ScanlineFlags.None;
            if ((convFlags & ConversionFlags.Swizzle) != 0)
                tflags |= ImageHelper.ScanlineFlags.Legacy;

            int index = 0;

            long checkSize = size;

            for (int arrayIndex = 0; arrayIndex < metadata.ArraySize; arrayIndex++)
            {
                uint d = metadata.Depth;
                // Else we need to go through each mips/depth slice to convert all scanlines.
                for (int level = 0; level < metadata.MipLevels; ++level)
                {
                    for (int slice = 0; slice < d; ++slice, ++index)
                    {
                        IntPtr pSrc = sourcePixelBuffers[index].DataPointer;
                        IntPtr pDest = destinationPixelBuffers[index].DataPointer;
                        checkSize -= sourcePixelBuffers[index].BufferStride;
                        if (checkSize < 0)
                            throw new InvalidOperationException("Unexpected end of buffer");

                        if (FormatHelper.IsCompressed(metadata.Format))
                        {
                            Utilities.CopyMemory(pDest, pSrc,
                                Math.Min(sourcePixelBuffers[index].BufferStride,
                                    destinationPixelBuffers[index].BufferStride));
                        }
                        else
                        {
                            int spitch = sourcePixelBuffers[index].RowStride;
                            int dpitch = destinationPixelBuffers[index].RowStride;

                            for (int h = 0; h < sourcePixelBuffers[index].Height; ++h)
                            {
                                if ((convFlags & ConversionFlags.Expand) != 0)
                                {
#if DIRECTX11_1
                                if ((convFlags & (ConversionFlags.Format565 | ConversionFlags.Format5551 | ConversionFlags.Format4444)) != 0)
#else
                                    if ((convFlags & (ConversionFlags.Format565 | ConversionFlags.Format5551)) != 0)
#endif
                                    {
                                        ImageHelper.ExpandScanline(pDest, dpitch, pSrc, spitch,
                                            (convFlags & ConversionFlags.Format565) != 0
                                                ? Format.B5G6R5_UNORM_PACK16
                                                : Format.B5G5R5A1_UNORM_PACK16, tflags);
                                    }
                                    else
                                    {
                                        var lformat = FindLegacyFormat(convFlags);
                                        LegacyExpandScanline(pDest, dpitch, metadata.Format, pSrc, spitch, lformat,
                                            pal8, tflags);
                                    }
                                }
                                else if ((convFlags & ConversionFlags.Swizzle) != 0)
                                {
                                    ImageHelper.SwizzleScanline(pDest, dpitch, pSrc, spitch, metadata.Format, tflags);
                                }
                                else
                                {
                                    if (pSrc != pDest)
                                        ImageHelper.CopyScanline(pDest, dpitch, pSrc, spitch, metadata.Format, tflags);
                                }

                                pSrc = (IntPtr)((byte*)pSrc + spitch);
                                pDest = (IntPtr)((byte*)pDest + dpitch);
                            }
                        }
                    }

                    if (d > 1)
                        d >>= 1;
                }
            }

            var img = ConvertPixelBuffersToRawData(ddsImage, destinationPixelBuffers);
            
            Utilities.FreeMemory(sourcePixelBuffers[0].DataPointer);
            Utilities.FreeMemory(destinationPixelBuffers[0].DataPointer);

            return img;
        }

        private static DdsImage ConvertPixelBuffersToRawData(DdsImage image, PixelBuffer[] buffers)
        {
            if (image.Description.MipLevels <= 1)
            {
                image.PixelBuffers = new FrameData[buffers.Length];
                for (int i = 0; i < buffers.Length; i++)
                {
                    var pixelBuffer = buffers[i];
                    var description = CreateDescriptionFromPixelBuffer(pixelBuffer, image.Description.Dimension);
                    var frameData = new FrameData(pixelBuffer.GetPixels<byte>(), description);
                    image.PixelBuffers[i] = frameData;
                }
            }
            else
            {
                image.PixelBuffers = new FrameData[1];
                image.MipLevels = new MipLevelData[image.Description.MipLevels];
                for (int i = 0; i < buffers.Length; i++)
                {
                    var pixelBuffer = buffers[i];
                    var description = CreateDescriptionFromPixelBuffer(pixelBuffer, image.Description.Dimension);
                    var mipData = new MipLevelData(description, pixelBuffer.MipLevel, pixelBuffer.GetPixels<byte>());
                    image.MipLevels[i] = mipData;
                }

                var mipData0 = image.GetMipLevelData(0);
                image.PixelBuffers[0] = new FrameData(mipData0.Pixels, mipData0.Description);
            }

            return image;
        }

        private static ImageDescription CreateDescriptionFromPixelBuffer(PixelBuffer buffer, TextureDimension dimension)
        {
            return new ImageDescription()
            {
                Width = buffer.Width,
                Height = buffer.Height,
                Depth = 1,
                Format = buffer.Format,
                Dimension = dimension,
                ArraySize = 1,
                MipLevels = 1
            };
        }

    }
}