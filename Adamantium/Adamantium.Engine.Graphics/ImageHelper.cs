using System;
using Adamantium.Core;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
    internal class ImageHelper
    {
        [Flags]
        internal enum ScanlineFlags
        {
            None = 0,
            SetAlpha = 0x1, // Set alpha channel to known opaque value
            Legacy = 0x2, // Enables specific legacy format conversion cases
        };

        /// <summary>
        /// Converts an image row with optional clearing of alpha value to 1.0
        /// </summary>
        /// <param name="pDestination"></param>
        /// <param name="outSize"></param>
        /// <param name="pSource"></param>
        /// <param name="inSize"></param>
        /// <param name="inFormat"></param>
        /// <param name="flags"></param>
        public unsafe static void ExpandScanline(IntPtr pDestination, int outSize, IntPtr pSource, int inSize, Format inFormat, ScanlineFlags flags)
        {
            switch (inFormat)
            {
                case Format.B5G6R5_UNORM_PACK16:
                    // DXGI.Format.B5G6R5_UNorm -> DXGI.Format.R8G8B8A8_UNorm
                    {
                        var sPtr = (ushort*)(pSource);
                        var dPtr = (uint*)(pDestination);

                        for (uint ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); icount += 2, ocount += 4)
                        {
                            ushort t = *(sPtr++);

                            uint t1 = (uint)(((t & 0xf800) >> 8) | ((t & 0xe000) >> 13));
                            uint t2 = (uint)(((t & 0x07e0) << 5) | ((t & 0x0600) >> 5));
                            uint t3 = (uint)(((t & 0x001f) << 19) | ((t & 0x001c) << 14));

                            *(dPtr++) = t1 | t2 | t3 | 0xff000000;
                        }
                    }
                    break;

                case Format.B5G5R5A1_UNORM_PACK16:
                    // DXGI.Format.B5G5R5A1_UNorm -> DXGI.Format.R8G8B8A8_UNorm
                    {
                        var sPtr = (ushort*)(pSource);
                        var dPtr = (uint*)(pDestination);

                        for (uint ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); icount += 2, ocount += 4)
                        {
                            ushort t = *(sPtr++);

                            uint t1 = (uint)(((t & 0x7c00) >> 7) | ((t & 0x7000) >> 12));
                            uint t2 = (uint)(((t & 0x03e0) << 6) | ((t & 0x0380) << 1));
                            uint t3 = (uint)(((t & 0x001f) << 19) | ((t & 0x001c) << 14));
                            uint ta = (uint)((flags & ScanlineFlags.SetAlpha) != 0 ? 0xff000000 : (((t & 0x8000) != 0 ? 0xff000000 : 0)));

                            *(dPtr++) = t1 | t2 | t3 | ta;
                        }
                    }
                    break;
                case Format.B4G4R4A4_UNORM_PACK16:
                    // DXGI.Format.B4G4R4A4_UNorm -> DXGI.Format.R8G8B8A8_UNorm
                    {
                        var sPtr = (ushort*)(pSource);
                        var dPtr = (uint*)(pDestination);

                        for (uint ocount = 0, icount = 0; ((icount < inSize) && (ocount < outSize)); icount += 2, ocount += 4)
                        {
                            ushort t = *(sPtr++);

                            uint t1 = (uint)(((t & 0x0f00) >> 4) | ((t & 0x0f00) >> 8));
                            uint t2 = (uint)(((t & 0x00f0) << 8) | ((t & 0x00f0) << 4));
                            uint t3 = (uint)(((t & 0x000f) << 20) | ((t & 0x000f) << 16));
                            uint ta = (flags & ScanlineFlags.SetAlpha) != 0 ? 0xff000000 : (uint)((((t & 0xf000) << 16) | ((t & 0xf000) << 12)));

                            *(dPtr++) = t1 | t2 | t3 | ta;
                        }
                    }
                    break;
                    // DXGI_1_2_FORMATS
            }
        }


        /// <summary>
        /// Copies an image row with optional clearing of alpha value to 1.0.
        /// </summary>
        /// <remarks>
        /// This method can be used in place as well, otherwise copies the image row unmodified.
        /// </remarks>
        /// <param name="pDestination">The destination buffer.</param>
        /// <param name="outSize">The destination size.</param>
        /// <param name="pSource">The source buffer.</param>
        /// <param name="inSize">The source size.</param>
        /// <param name="format">The <see cref="Format"/> of the source scanline.</param>
        /// <param name="flags">Scanline flags used when copying the scanline.</param>
        internal static unsafe void CopyScanline(IntPtr pDestination, int outSize, IntPtr pSource, int inSize, Format format, ScanlineFlags flags)
        {
            if ((flags & ScanlineFlags.SetAlpha) != 0)
            {
                switch (format)
                {
                    //-----------------------------------------------------------------------------
                    case Format.R32G32B32A32_SFLOAT:
                    case Format.R32G32B32A32_UINT:
                    case Format.R32G32B32A32_SINT:
                        {
                            uint alpha;
                            if (format == Format.R32G32B32A32_SFLOAT)
                                alpha = 0x3f800000;
                            else if (format == Format.R32G32B32A32_SINT)
                                alpha = 0x7fffffff;
                            else
                                alpha = 0xffffffff;

                            if (pDestination == pSource)
                            {
                                var dPtr = (uint*)(pDestination);
                                for (int count = 0; count < outSize; count += 16)
                                {
                                    dPtr += 3;
                                    *(dPtr++) = alpha;
                                }
                            }
                            else
                            {
                                var sPtr = (uint*)(pSource);
                                var dPtr = (uint*)(pDestination);
                                int size = Math.Min(outSize, inSize);
                                for (int count = 0; count < size; count += 16)
                                {
                                    *(dPtr++) = *(sPtr++);
                                    *(dPtr++) = *(sPtr++);
                                    *(dPtr++) = *(sPtr++);
                                    *(dPtr++) = alpha;
                                    sPtr++;
                                }
                            }
                        }
                        return;

                    //-----------------------------------------------------------------------------
                    case Format.R16G16B16A16_SFLOAT:
                    case Format.R16G16B16A16_UNORM:
                    case Format.R16G16B16A16_UINT:
                    case Format.R16G16B16A16_SNORM:
                    case Format.R16G16B16A16_SINT:
                        {
                            ushort alpha;
                            if (format == Format.R16G16B16A16_SFLOAT)
                                alpha = 0x3c00;
                            else if (format == Format.R16G16B16A16_SNORM || format == Format.R16G16B16A16_SINT)
                                alpha = 0x7fff;
                            else
                                alpha = 0xffff;

                            if (pDestination == pSource)
                            {
                                var dPtr = (ushort*)(pDestination);
                                for (int count = 0; count < outSize; count += 8)
                                {
                                    dPtr += 3;
                                    *(dPtr++) = alpha;
                                }
                            }
                            else
                            {
                                var sPtr = (ushort*)(pSource);
                                var dPtr = (ushort*)(pDestination);
                                int size = Math.Min(outSize, inSize);
                                for (int count = 0; count < size; count += 8)
                                {
                                    *(dPtr++) = *(sPtr++);
                                    *(dPtr++) = *(sPtr++);
                                    *(dPtr++) = *(sPtr++);
                                    *(dPtr++) = alpha;
                                    sPtr++;
                                }
                            }
                        }
                        return;

                    //-----------------------------------------------------------------------------
                    case Format.A2R10G10B10_UINT_PACK32:
                    case Format.A2R10G10B10_UNORM_PACK32:
                        {
                            if (pDestination == pSource)
                            {
                                var dPtr = (uint*)(pDestination);
                                for (int count = 0; count < outSize; count += 4)
                                {
                                    *dPtr |= 0xC0000000;
                                    ++dPtr;
                                }
                            }
                            else
                            {
                                var sPtr = (uint*)(pSource);
                                var dPtr = (uint*)(pDestination);
                                int size = Math.Min(outSize, inSize);
                                for (int count = 0; count < size; count += 4)
                                {
                                    *(dPtr++) = *(sPtr++) | 0xC0000000;
                                }
                            }
                        }
                        return;

                    //-----------------------------------------------------------------------------
                    case Format.R8G8B8A8_UNORM:
                    case Format.R8G8B8A8_SRGB:
                    case Format.R8G8B8A8_UINT:
                    case Format.R8G8B8A8_SNORM:
                    case Format.R8G8B8A8_SINT:
                    case Format.B8G8R8A8_UNORM:
                    case Format.B8G8R8A8_SRGB:
                        {
                            uint alpha = (format == Format.R8G8B8A8_SNORM || format == Format.R8G8B8A8_SINT) ? 0x7f000000 : 0xff000000;

                            if (pDestination == pSource)
                            {
                                var dPtr = (uint*)(pDestination);
                                for (int count = 0; count < outSize; count += 4)
                                {
                                    uint t = *dPtr & 0xFFFFFF;
                                    t |= alpha;
                                    *(dPtr++) = t;
                                }
                            }
                            else
                            {
                                var sPtr = (uint*)(pSource);
                                var dPtr = (uint*)(pDestination);
                                int size = Math.Min(outSize, inSize);
                                for (int count = 0; count < size; count += 4)
                                {
                                    uint t = *(sPtr++) & 0xFFFFFF;
                                    t |= alpha;
                                    *(dPtr++) = t;
                                }
                            }
                        }
                        return;

                    //-----------------------------------------------------------------------------
                    case Format.B5G5R5A1_UNORM_PACK16:
                        {
                            if (pDestination == pSource)
                            {
                                var dPtr = (ushort*)(pDestination);
                                for (int count = 0; count < outSize; count += 2)
                                {
                                    *(dPtr++) |= 0x8000;
                                }
                            }
                            else
                            {
                                var sPtr = (ushort*)(pSource);
                                var dPtr = (ushort*)(pDestination);
                                int size = Math.Min(outSize, inSize);
                                for (int count = 0; count < size; count += 2)
                                {
                                    *(dPtr++) = (ushort)(*(sPtr++) | 0x8000);
                                }
                            }
                        }
                        return;

                    //-----------------------------------------------------------------------------
                    //case Format.A8_UNorm:
                    //    Utilities.ClearMemory(pDestination, 0xff, outSize);
                    //    return;

                    //-----------------------------------------------------------------------------
                    case Format.B4G4R4A4_UNORM_PACK16:
                        {
                            if (pDestination == pSource)
                            {
                                var dPtr = (ushort*) (pDestination);
                                for (int count = 0; count < outSize; count += 2)
                                {
                                    *(dPtr++) |= 0xF000;
                                }
                            }
                            else
                            {
                                var sPtr = (ushort*) (pSource);
                                var dPtr = (ushort*) (pDestination);
                                int size = Math.Min(outSize, inSize);
                                for (int count = 0; count < size; count += 2)
                                {
                                    *(dPtr++) = (ushort) (*(sPtr++) | 0xF000);
                                }
                            }
                        }
                        return;
                        // DXGI_1_2_FORMATS
                }
            }

            // Fall-through case is to just use memcpy (assuming this is not an in-place operation)
            if (pDestination == pSource)
                return;

            Utilities.CopyMemory(pDestination, pSource, Math.Min(outSize, inSize));
        }

        /// <summary>
        /// Swizzles (RGB &lt;-&gt; BGR) an image row with optional clearing of alpha value to 1.0.
        /// </summary>
        /// <param name="pDestination">The destination buffer.</param>
        /// <param name="outSize">The destination size.</param>
        /// <param name="pSource">The source buffer.</param>
        /// <param name="inSize">The source size.</param>
        /// <param name="format">The <see cref="Format"/> of the source scanline.</param>
        /// <param name="flags">Scanline flags used when copying the scanline.</param>
        /// <remarks>
        /// This method can be used in place as well, otherwise copies the image row unmodified.
        /// </remarks>
        internal static unsafe void SwizzleScanline(IntPtr pDestination, int outSize, IntPtr pSource, int inSize, Format format, ScanlineFlags flags)
        {
            switch (format)
            {
                //---------------------------------------------------------------------------------
                case Format.A2R10G10B10_UNORM_PACK32:
                case Format.A2R10G10B10_UINT_PACK32:
                    if ((flags & ScanlineFlags.Legacy) != 0)
                    {
                        // Swap Red (R) and Blue (B) channel (used for D3DFMT_A2R10G10B10 legacy sources)
                        if (pDestination == pSource)
                        {
                            var dPtr = (uint*)(pDestination);
                            for (int count = 0; count < outSize; count += 4)
                            {
                                uint t = *dPtr;

                                uint t1 = (t & 0x3ff00000) >> 20;
                                uint t2 = (t & 0x000003ff) << 20;
                                uint t3 = (t & 0x000ffc00);
                                uint ta = (flags & ScanlineFlags.SetAlpha) != 0 ? 0xC0000000 : (t & 0xC0000000);

                                *(dPtr++) = t1 | t2 | t3 | ta;
                            }
                        }
                        else
                        {
                            var sPtr = (uint*)(pSource);
                            var dPtr = (uint*)(pDestination);
                            int size = Math.Min(outSize, inSize);
                            for (int count = 0; count < size; count += 4)
                            {
                                uint t = *(sPtr++);

                                uint t1 = (t & 0x3ff00000) >> 20;
                                uint t2 = (t & 0x000003ff) << 20;
                                uint t3 = (t & 0x000ffc00);
                                uint ta = (flags & ScanlineFlags.SetAlpha) != 0 ? 0xC0000000 : (t & 0xC0000000);

                                *(dPtr++) = t1 | t2 | t3 | ta;
                            }
                        }
                        return;
                    }
                    break;

                //---------------------------------------------------------------------------------
                case Format.R8G8B8A8_UNORM:
                case Format.R8G8B8A8_SRGB:
                case Format.B8G8R8A8_UNORM:
                case Format.B8G8R8_UNORM:
                case Format.B8G8R8A8_SRGB:
                case Format.B8G8R8_SRGB:
                    // Swap Red (R) and Blue (B) channels (used to convert from DXGI 1.1 BGR formats to DXGI 1.0 RGB)
                    if (pDestination == pSource)
                    {
                        var dPtr = (uint*)(pDestination);
                        for (int count = 0; count < outSize; count += 4)
                        {
                            uint t = *dPtr;

                            uint t1 = (t & 0x00ff0000) >> 16;
                            uint t2 = (t & 0x000000ff) << 16;
                            uint t3 = (t & 0x0000ff00);
                            uint ta = (flags & ScanlineFlags.SetAlpha) != 0 ? 0xff000000 : (t & 0xFF000000);

                            *(dPtr++) = t1 | t2 | t3 | ta;
                        }
                    }
                    else
                    {
                        var sPtr = (uint*)(pSource);
                        var dPtr = (uint*)(pDestination);
                        int size = Math.Min(outSize, inSize);
                        for (int count = 0; count < size; count += 4)
                        {
                            uint t = *(sPtr++);

                            uint t1 = (t & 0x00ff0000) >> 16;
                            uint t2 = (t & 0x000000ff) << 16;
                            uint t3 = (t & 0x0000ff00);
                            uint ta = (flags & ScanlineFlags.SetAlpha) != 0 ? 0xff000000 : (t & 0xFF000000);

                            *(dPtr++) = t1 | t2 | t3 | ta;
                        }
                    }
                    return;
            }

            // Fall-through case is to just use memcpy (assuming this is not an in-place operation)
            if (pDestination == pSource)
                return;

            Utilities.CopyMemory(pDestination, pSource, Math.Min(outSize, inSize));
        }

        public static unsafe void CopyScanline(IntPtr pDestination, IntPtr pSource, int size)
        {
            Utilities.CopyMemory(pDestination, pSource, size);
        }
    }
}
