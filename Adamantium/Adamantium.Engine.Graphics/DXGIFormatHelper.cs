using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Helper to use with <see cref="DXGIFormat"/>.
    /// </summary>
    public static class DXGIFormatHelper
    {
        private static readonly int[] sizeOfInBits = new int[256];
        private static readonly bool[] compressedDXGIFormats = new bool[256];
        private static readonly bool[] srgbDXGIFormats = new bool[256];
        private static readonly bool[] typelessDXGIFormats = new bool[256];

        /// <summary>
        /// Calculates the size of a <see cref="DXGIFormat"/> in bytes. Can be 0 for compressed DXGIFormat (as they are less than 1 byte)
        /// </summary>
        /// <param name="DXGIFormat">The DXGI DXGIFormat.</param>
        /// <returns>size of in bytes</returns>
        public static int SizeOfInBytes(this DXGIFormat DXGIFormat)
        {
            var sizeInBits = SizeOfInBits(DXGIFormat);
            return sizeInBits >> 3;
        }

        /// <summary>
        /// Calculates the size of a <see cref="DXGIFormat"/> in bits.
        /// </summary>
        /// <param name="DXGIFormat">The DXGI DXGIFormat.</param>
        /// <returns>size of in bits</returns>
        public static int SizeOfInBits(this DXGIFormat DXGIFormat)
        {
            return sizeOfInBits[(int)DXGIFormat];
        }

        /// <summary>
        /// Returns true if the <see cref="DXGIFormat"/> is valid.
        /// </summary>
        /// <param name="DXGIFormat">A DXGIFormat to validate</param>
        /// <returns>True if the <see cref="DXGIFormat"/> is valid.</returns>
        public static bool IsValid(this DXGIFormat DXGIFormat)
        {
            return ((int)(DXGIFormat) >= 1 && (int)(DXGIFormat) <= 115);
        }

        /// <summary>
        /// Returns true if the <see cref="DXGIFormat"/> is a compressed DXGIFormat.
        /// </summary>
        /// <param name="DXGIFormat">The DXGIFormat to check for compressed.</param>
        /// <returns>True if the <see cref="DXGIFormat"/> is a compressed DXGIFormat</returns>
        public static bool IsCompressed(this DXGIFormat DXGIFormat)
        {
            return compressedDXGIFormats[(int)DXGIFormat];
        }

        /// <summary>
        /// Determines whether the specified <see cref="DXGIFormat"/> is packed.
        /// </summary>
        /// <param name="DXGIFormat">The DXGI DXGIFormat.</param>
        /// <returns><c>true</c> if the specified <see cref="DXGIFormat"/> is packed; otherwise, <c>false</c>.</returns>
        public static bool IsPacked(this DXGIFormat DXGIFormat)
        {
            return ((DXGIFormat == DXGIFormat.R8G8_B8G8_UNorm) || (DXGIFormat == DXGIFormat.G8R8_G8B8_UNorm));
        }

        /// <summary>
        /// Determines whether the specified <see cref="DXGIFormat"/> is video.
        /// </summary>
        /// <param name="DXGIFormat">The <see cref="DXGIFormat"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="DXGIFormat"/> is video; otherwise, <c>false</c>.</returns>
        public static bool IsVideo(this DXGIFormat DXGIFormat)
        {
            switch (DXGIFormat)
            {
                case DXGIFormat.AYUV:
                case DXGIFormat.Y410:
                case DXGIFormat.Y416:
                case DXGIFormat.NV12:
                case DXGIFormat.P010:
                case DXGIFormat.P016:
                case DXGIFormat.YUY2:
                case DXGIFormat.Y210:
                case DXGIFormat.Y216:
                case DXGIFormat.NV11:
                    // These video DXGIFormats can be used with the 3D pipeline through special view mappings
                    return true;

                case DXGIFormat.Opaque420:
                case DXGIFormat.AI44:
                case DXGIFormat.IA44:
                case DXGIFormat.P8:
                case DXGIFormat.A8P8:
                    // These are limited use video DXGIFormats not usable in any way by the 3D pipeline
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether the specified <see cref="DXGIFormat"/> is a SRGB DXGIFormat.
        /// </summary>
        /// <param name="DXGIFormat">The <see cref="DXGIFormat"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="DXGIFormat"/> is a SRGB DXGIFormat; otherwise, <c>false</c>.</returns>
        public static bool IsSRgb(this DXGIFormat DXGIFormat)
        {
            return srgbDXGIFormats[(int)DXGIFormat];
        }

        /// <summary>
        /// Determines whether the specified <see cref="DXGIFormat"/> is typeless.
        /// </summary>
        /// <param name="DXGIFormat">The <see cref="DXGIFormat"/>.</param>
        /// <returns><c>true</c> if the specified <see cref="DXGIFormat"/> is typeless; otherwise, <c>false</c>.</returns>
        public static bool IsTypeless(this DXGIFormat DXGIFormat)
        {
            return typelessDXGIFormats[(int)DXGIFormat];
        }

        /// <summary>
        /// Computes the scanline count (number of scanlines).
        /// </summary>
        /// <param name="DXGIFormat">The <see cref="DXGIFormat"/>.</param>
        /// <param name="height">The height.</param>
        /// <returns>The scanline count.</returns>
        public static int ComputeScanlineCount(this DXGIFormat DXGIFormat, int height)
        {
            switch (DXGIFormat)
            {
                case DXGIFormat.BC1_Typeless:
                case DXGIFormat.BC1_UNorm:
                case DXGIFormat.BC1_UNorm_SRgb:
                case DXGIFormat.BC2_Typeless:
                case DXGIFormat.BC2_UNorm:
                case DXGIFormat.BC2_UNorm_SRgb:
                case DXGIFormat.BC3_Typeless:
                case DXGIFormat.BC3_UNorm:
                case DXGIFormat.BC3_UNorm_SRgb:
                case DXGIFormat.BC4_Typeless:
                case DXGIFormat.BC4_UNorm:
                case DXGIFormat.BC4_SNorm:
                case DXGIFormat.BC5_Typeless:
                case DXGIFormat.BC5_UNorm:
                case DXGIFormat.BC5_SNorm:
                case DXGIFormat.BC6H_Typeless:
                case DXGIFormat.BC6H_Uf16:
                case DXGIFormat.BC6H_Sf16:
                case DXGIFormat.BC7_Typeless:
                case DXGIFormat.BC7_UNorm:
                case DXGIFormat.BC7_UNorm_SRgb:
                    return Math.Max(1, (height + 3) / 4);

                default:
                    return height;
            }
        }

        /// <summary>
        /// Static initializer to speed up size calculation (not sure the JIT is enough "smart" for this kind of thing).
        /// </summary>
        static DXGIFormatHelper()
        {
            InitDXGIFormat(new[] { DXGIFormat.R1_UNorm }, 1);

            InitDXGIFormat(new[] { DXGIFormat.A8_UNorm, DXGIFormat.R8_SInt, DXGIFormat.R8_SNorm, DXGIFormat.R8_Typeless, DXGIFormat.R8_UInt, DXGIFormat.R8_UNorm }, 8);

            InitDXGIFormat(new[] {
                DXGIFormat.B5G5R5A1_UNorm,
                DXGIFormat.B5G6R5_UNorm,
                DXGIFormat.D16_UNorm,
                DXGIFormat.R16_Float,
                DXGIFormat.R16_SInt,
                DXGIFormat.R16_SNorm,
                DXGIFormat.R16_Typeless,
                DXGIFormat.R16_UInt,
                DXGIFormat.R16_UNorm,
                DXGIFormat.R8G8_SInt,
                DXGIFormat.R8G8_SNorm,
                DXGIFormat.R8G8_Typeless,
                DXGIFormat.R8G8_UInt,
                DXGIFormat.R8G8_UNorm,
                DXGIFormat.B4G4R4A4_UNorm,
            }, 16);

            InitDXGIFormat(new[] {
                DXGIFormat.B8G8R8X8_Typeless,
                DXGIFormat.B8G8R8X8_UNorm,
                DXGIFormat.B8G8R8X8_UNorm_SRgb,
                DXGIFormat.D24_UNorm_S8_UInt,
                DXGIFormat.D32_Float,
                DXGIFormat.D32_Float_S8X24_UInt,
                DXGIFormat.G8R8_G8B8_UNorm,
                DXGIFormat.R10G10B10_Xr_Bias_A2_UNorm,
                DXGIFormat.R10G10B10A2_Typeless,
                DXGIFormat.R10G10B10A2_UInt,
                DXGIFormat.R10G10B10A2_UNorm,
                DXGIFormat.R11G11B10_Float,
                DXGIFormat.R16G16_Float,
                DXGIFormat.R16G16_SInt,
                DXGIFormat.R16G16_SNorm,
                DXGIFormat.R16G16_Typeless,
                DXGIFormat.R16G16_UInt,
                DXGIFormat.R16G16_UNorm,
                DXGIFormat.R24_UNorm_X8_Typeless,
                DXGIFormat.R24G8_Typeless,
                DXGIFormat.R32_Float,
                DXGIFormat.R32_Float_X8X24_Typeless,
                DXGIFormat.R32_SInt,
                DXGIFormat.R32_Typeless,
                DXGIFormat.R32_UInt,
                DXGIFormat.R8G8_B8G8_UNorm,
                DXGIFormat.R8G8B8A8_SInt,
                DXGIFormat.R8G8B8A8_SNorm,
                DXGIFormat.R8G8B8A8_Typeless,
                DXGIFormat.R8G8B8A8_UInt,
                DXGIFormat.R8G8B8A8_UNorm,
                DXGIFormat.R8G8B8A8_UNorm_SRgb,
                DXGIFormat.B8G8R8A8_Typeless,
                DXGIFormat.B8G8R8A8_UNorm,
                DXGIFormat.B8G8R8A8_UNorm_SRgb,
                DXGIFormat.R9G9B9E5_Sharedexp,
                DXGIFormat.X24_Typeless_G8_UInt,
                DXGIFormat.X32_Typeless_G8X24_UInt,
            }, 32);

            InitDXGIFormat(new[] {
                DXGIFormat.R16G16B16A16_Float,
                DXGIFormat.R16G16B16A16_SInt,
                DXGIFormat.R16G16B16A16_SNorm,
                DXGIFormat.R16G16B16A16_Typeless,
                DXGIFormat.R16G16B16A16_UInt,
                DXGIFormat.R16G16B16A16_UNorm,
                DXGIFormat.R32G32_Float,
                DXGIFormat.R32G32_SInt,
                DXGIFormat.R32G32_Typeless,
                DXGIFormat.R32G32_UInt,
                DXGIFormat.R32G8X24_Typeless,
            }, 64);

            InitDXGIFormat(new[] {
                DXGIFormat.R32G32B32_Float,
                DXGIFormat.R32G32B32_SInt,
                DXGIFormat.R32G32B32_Typeless,
                DXGIFormat.R32G32B32_UInt,
            }, 96);

            InitDXGIFormat(new[] {
                DXGIFormat.R32G32B32A32_Float,
                DXGIFormat.R32G32B32A32_SInt,
                DXGIFormat.R32G32B32A32_Typeless,
                DXGIFormat.R32G32B32A32_UInt,
            }, 128);

            InitDXGIFormat(new[] {
                DXGIFormat.BC1_Typeless,
                DXGIFormat.BC1_UNorm,
                DXGIFormat.BC1_UNorm_SRgb,
                DXGIFormat.BC4_SNorm,
                DXGIFormat.BC4_Typeless,
                DXGIFormat.BC4_UNorm,
            }, 4);

            InitDXGIFormat(new[] {
                DXGIFormat.BC2_Typeless,
                DXGIFormat.BC2_UNorm,
                DXGIFormat.BC2_UNorm_SRgb,
                DXGIFormat.BC3_Typeless,
                DXGIFormat.BC3_UNorm,
                DXGIFormat.BC3_UNorm_SRgb,
                DXGIFormat.BC5_SNorm,
                DXGIFormat.BC5_Typeless,
                DXGIFormat.BC5_UNorm,
                DXGIFormat.BC6H_Sf16,
                DXGIFormat.BC6H_Typeless,
                DXGIFormat.BC6H_Uf16,
                DXGIFormat.BC7_Typeless,
                DXGIFormat.BC7_UNorm,
                DXGIFormat.BC7_UNorm_SRgb,
            }, 8);


            // Init compressed DXGIFormats
            InitDefaults(new[]
                             {
                                 DXGIFormat.BC1_Typeless,
                                 DXGIFormat.BC1_UNorm,
                                 DXGIFormat.BC1_UNorm_SRgb,
                                 DXGIFormat.BC2_Typeless,
                                 DXGIFormat.BC2_UNorm,
                                 DXGIFormat.BC2_UNorm_SRgb,
                                 DXGIFormat.BC3_Typeless,
                                 DXGIFormat.BC3_UNorm,
                                 DXGIFormat.BC3_UNorm_SRgb,
                                 DXGIFormat.BC4_Typeless,
                                 DXGIFormat.BC4_UNorm,
                                 DXGIFormat.BC4_SNorm,
                                 DXGIFormat.BC5_Typeless,
                                 DXGIFormat.BC5_UNorm,
                                 DXGIFormat.BC5_SNorm,
                                 DXGIFormat.BC6H_Typeless,
                                 DXGIFormat.BC6H_Uf16,
                                 DXGIFormat.BC6H_Sf16,
                                 DXGIFormat.BC7_Typeless,
                                 DXGIFormat.BC7_UNorm,
                                 DXGIFormat.BC7_UNorm_SRgb,
                             }, compressedDXGIFormats);

            // Init srgb DXGIFormats
            InitDefaults(new[]
                             {
                                 DXGIFormat.R8G8B8A8_UNorm_SRgb,
                                 DXGIFormat.BC1_UNorm_SRgb,
                                 DXGIFormat.BC2_UNorm_SRgb,
                                 DXGIFormat.BC3_UNorm_SRgb,
                                 DXGIFormat.B8G8R8A8_UNorm_SRgb,
                                 DXGIFormat.B8G8R8X8_UNorm_SRgb,
                                 DXGIFormat.BC7_UNorm_SRgb,
                             }, srgbDXGIFormats);

            // Init typeless DXGIFormats
            InitDefaults(new[]
                             {
                                 DXGIFormat.R32G32B32A32_Typeless,
                                 DXGIFormat.R32G32B32_Typeless,
                                 DXGIFormat.R16G16B16A16_Typeless,
                                 DXGIFormat.R32G32_Typeless,
                                 DXGIFormat.R32G8X24_Typeless,
                                 DXGIFormat.R10G10B10A2_Typeless,
                                 DXGIFormat.R8G8B8A8_Typeless,
                                 DXGIFormat.R16G16_Typeless,
                                 DXGIFormat.R32_Typeless,
                                 DXGIFormat.R24G8_Typeless,
                                 DXGIFormat.R8G8_Typeless,
                                 DXGIFormat.R16_Typeless,
                                 DXGIFormat.R8_Typeless,
                                 DXGIFormat.BC1_Typeless,
                                 DXGIFormat.BC2_Typeless,
                                 DXGIFormat.BC3_Typeless,
                                 DXGIFormat.BC4_Typeless,
                                 DXGIFormat.BC5_Typeless,
                                 DXGIFormat.B8G8R8A8_Typeless,
                                 DXGIFormat.B8G8R8X8_Typeless,
                                 DXGIFormat.BC6H_Typeless,
                                 DXGIFormat.BC7_Typeless,
                             }, typelessDXGIFormats);


        }

        private static void InitDXGIFormat(IEnumerable<DXGIFormat> DXGIFormats, int bitCount)
        {
            foreach (var DXGIFormat in DXGIFormats)
                sizeOfInBits[(int)DXGIFormat] = bitCount;
        }

        private static void InitDefaults(IEnumerable<DXGIFormat> DXGIFormats, bool[] outputArray)
        {
            foreach (var DXGIFormat in DXGIFormats)
                outputArray[(int)DXGIFormat] = true;
        }
    }
}
