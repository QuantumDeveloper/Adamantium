namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// Resource data formats, including fully-typed and typeless formats. A list of
    /// modifiers at the bottom of the page more fully describes each format type.
    /// </summary>
    public enum DXGIFormat
    {
        /// <summary>
        /// The format is not known.
        /// </summary>
        Unknown = 0,
        //
        // Сводка:
        //     A four-component, 128-bit typeless format that supports 32 bits per channel including
        //     alpha. ?
        R32G32B32A32_Typeless = 1,
        //
        // Сводка:
        //     A four-component, 128-bit floating-point format that supports 32 bits per channel
        //     including alpha. 1,5,8
        R32G32B32A32_Float = 2,
        //
        // Сводка:
        //     A four-component, 128-bit unsigned-integer format that supports 32 bits per channel
        //     including alpha. ?
        R32G32B32A32_UInt = 3,
        //
        // Сводка:
        //     A four-component, 128-bit signed-integer format that supports 32 bits per channel
        //     including alpha. ?
        R32G32B32A32_SInt = 4,
        //
        // Сводка:
        //     A three-component, 96-bit typeless format that supports 32 bits per color channel.
        R32G32B32_Typeless = 5,
        //
        // Сводка:
        //     A three-component, 96-bit floating-point format that supports 32 bits per color
        //     channel.5,8
        R32G32B32_Float = 6,
        //
        // Сводка:
        //     A three-component, 96-bit unsigned-integer format that supports 32 bits per color
        //     channel.
        R32G32B32_UInt = 7,
        //
        // Сводка:
        //     A three-component, 96-bit signed-integer format that supports 32 bits per color
        //     channel.
        R32G32B32_SInt = 8,
        //
        // Сводка:
        //     A four-component, 64-bit typeless format that supports 16 bits per channel including
        //     alpha.
        R16G16B16A16_Typeless = 9,
        //
        // Сводка:
        //     A four-component, 64-bit floating-point format that supports 16 bits per channel
        //     including alpha.5,7
        R16G16B16A16_Float = 10,
        //
        // Сводка:
        //     A four-component, 64-bit unsigned-normalized-integer format that supports 16
        //     bits per channel including alpha.
        R16G16B16A16_UNorm = 11,
        //
        // Сводка:
        //     A four-component, 64-bit unsigned-integer format that supports 16 bits per channel
        //     including alpha.
        R16G16B16A16_UInt = 12,
        //
        // Сводка:
        //     A four-component, 64-bit signed-normalized-integer format that supports 16 bits
        //     per channel including alpha.
        R16G16B16A16_SNorm = 13,
        //
        // Сводка:
        //     A four-component, 64-bit signed-integer format that supports 16 bits per channel
        //     including alpha.
        R16G16B16A16_SInt = 14,
        //
        // Сводка:
        //     A two-component, 64-bit typeless format that supports 32 bits for the red channel
        //     and 32 bits for the green channel.
        R32G32_Typeless = 15,
        //
        // Сводка:
        //     A two-component, 64-bit floating-point format that supports 32 bits for the red
        //     channel and 32 bits for the green channel.5,8
        R32G32_Float = 16,
        //
        // Сводка:
        //     A two-component, 64-bit unsigned-integer format that supports 32 bits for the
        //     red channel and 32 bits for the green channel.
        R32G32_UInt = 17,
        //
        // Сводка:
        //     A two-component, 64-bit signed-integer format that supports 32 bits for the red
        //     channel and 32 bits for the green channel.
        R32G32_SInt = 18,
        //
        // Сводка:
        //     A two-component, 64-bit typeless format that supports 32 bits for the red channel,
        //     8 bits for the green channel, and 24 bits are unused.
        R32G8X24_Typeless = 19,
        //
        // Сводка:
        //     A 32-bit floating-point component, and two unsigned-integer components (with
        //     an additional 32 bits). This format supports 32-bit depth, 8-bit stencil, and
        //     24 bits are unused.?
        D32_Float_S8X24_UInt = 20,
        //
        // Сводка:
        //     A 32-bit floating-point component, and two typeless components (with an additional
        //     32 bits). This format supports 32-bit red channel, 8 bits are unused, and 24
        //     bits are unused.?
        R32_Float_X8X24_Typeless = 21,
        //
        // Сводка:
        //     A 32-bit typeless component, and two unsigned-integer components (with an additional
        //     32 bits). This format has 32 bits unused, 8 bits for green channel, and 24 bits
        //     are unused.
        X32_Typeless_G8X24_UInt = 22,
        //
        // Сводка:
        //     A four-component, 32-bit typeless format that supports 10 bits for each color
        //     and 2 bits for alpha.
        R10G10B10A2_Typeless = 23,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-normalized-integer format that supports 10
        //     bits for each color and 2 bits for alpha.
        R10G10B10A2_UNorm = 24,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-integer format that supports 10 bits for each
        //     color and 2 bits for alpha.
        R10G10B10A2_UInt = 25,
        //
        // Сводка:
        //     Three partial-precision floating-point numbers encoded into a single 32-bit value
        //     (a variant of s10e5, which is sign bit, 10-bit mantissa, and 5-bit biased (15)
        //     exponent). There are no sign bits, and there is a 5-bit biased (15) exponent
        //     for each channel, 6-bit mantissa for R and G, and a 5-bit mantissa for B, as
        //     shown in the following illustration.5,7
        R11G11B10_Float = 26,
        //
        // Сводка:
        //     A four-component, 32-bit typeless format that supports 8 bits per channel including
        //     alpha.
        R8G8B8A8_Typeless = 27,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-normalized-integer format that supports 8 bits
        //     per channel including alpha.
        R8G8B8A8_UNorm = 28,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-normalized integer sRGB format that supports
        //     8 bits per channel including alpha.
        R8G8B8A8_UNorm_SRgb = 29,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-integer format that supports 8 bits per channel
        //     including alpha.
        R8G8B8A8_UInt = 30,
        //
        // Сводка:
        //     A four-component, 32-bit signed-normalized-integer format that supports 8 bits
        //     per channel including alpha.
        R8G8B8A8_SNorm = 31,
        //
        // Сводка:
        //     A four-component, 32-bit signed-integer format that supports 8 bits per channel
        //     including alpha.
        R8G8B8A8_SInt = 32,
        //
        // Сводка:
        //     A two-component, 32-bit typeless format that supports 16 bits for the red channel
        //     and 16 bits for the green channel.
        R16G16_Typeless = 33,
        //
        // Сводка:
        //     A two-component, 32-bit floating-point format that supports 16 bits for the red
        //     channel and 16 bits for the green channel.5,7
        R16G16_Float = 34,
        //
        // Сводка:
        //     A two-component, 32-bit unsigned-normalized-integer format that supports 16 bits
        //     each for the green and red channels.
        R16G16_UNorm = 35,
        //
        // Сводка:
        //     A two-component, 32-bit unsigned-integer format that supports 16 bits for the
        //     red channel and 16 bits for the green channel.
        R16G16_UInt = 36,
        //
        // Сводка:
        //     A two-component, 32-bit signed-normalized-integer format that supports 16 bits
        //     for the red channel and 16 bits for the green channel.
        R16G16_SNorm = 37,
        //
        // Сводка:
        //     A two-component, 32-bit signed-integer format that supports 16 bits for the red
        //     channel and 16 bits for the green channel.
        R16G16_SInt = 38,
        //
        // Сводка:
        //     A single-component, 32-bit typeless format that supports 32 bits for the red
        //     channel.
        R32_Typeless = 39,
        //
        // Сводка:
        //     A single-component, 32-bit floating-point format that supports 32 bits for depth.5,8
        D32_Float = 40,
        //
        // Сводка:
        //     A single-component, 32-bit floating-point format that supports 32 bits for the
        //     red channel.5,8
        R32_Float = 41,
        //
        // Сводка:
        //     A single-component, 32-bit unsigned-integer format that supports 32 bits for
        //     the red channel.
        R32_UInt = 42,
        //
        // Сводка:
        //     A single-component, 32-bit signed-integer format that supports 32 bits for the
        //     red channel.
        R32_SInt = 43,
        //
        // Сводка:
        //     A two-component, 32-bit typeless format that supports 24 bits for the red channel
        //     and 8 bits for the green channel.
        R24G8_Typeless = 44,
        //
        // Сводка:
        //     A 32-bit z-buffer format that supports 24 bits for depth and 8 bits for stencil.
        D24_UNorm_S8_UInt = 45,
        //
        // Сводка:
        //     A 32-bit format, that contains a 24 bit, single-component, unsigned-normalized
        //     integer, with an additional typeless 8 bits. This format has 24 bits red channel
        //     and 8 bits unused.
        R24_UNorm_X8_Typeless = 46,
        //
        // Сводка:
        //     A 32-bit format, that contains a 24 bit, single-component, typeless format, with
        //     an additional 8 bit unsigned integer component. This format has 24 bits unused
        //     and 8 bits green channel.
        X24_Typeless_G8_UInt = 47,
        //
        // Сводка:
        //     A two-component, 16-bit typeless format that supports 8 bits for the red channel
        //     and 8 bits for the green channel.
        R8G8_Typeless = 48,
        //
        // Сводка:
        //     A two-component, 16-bit unsigned-normalized-integer format that supports 8 bits
        //     for the red channel and 8 bits for the green channel.
        R8G8_UNorm = 49,
        //
        // Сводка:
        //     A two-component, 16-bit unsigned-integer format that supports 8 bits for the
        //     red channel and 8 bits for the green channel.
        R8G8_UInt = 50,
        //
        // Сводка:
        //     A two-component, 16-bit signed-normalized-integer format that supports 8 bits
        //     for the red channel and 8 bits for the green channel.
        R8G8_SNorm = 51,
        //
        // Сводка:
        //     A two-component, 16-bit signed-integer format that supports 8 bits for the red
        //     channel and 8 bits for the green channel.
        R8G8_SInt = 52,
        //
        // Сводка:
        //     A single-component, 16-bit typeless format that supports 16 bits for the red
        //     channel.
        R16_Typeless = 53,
        //
        // Сводка:
        //     A single-component, 16-bit floating-point format that supports 16 bits for the
        //     red channel.5,7
        R16_Float = 54,
        //
        // Сводка:
        //     A single-component, 16-bit unsigned-normalized-integer format that supports 16
        //     bits for depth.
        D16_UNorm = 55,
        //
        // Сводка:
        //     A single-component, 16-bit unsigned-normalized-integer format that supports 16
        //     bits for the red channel.
        R16_UNorm = 56,
        //
        // Сводка:
        //     A single-component, 16-bit unsigned-integer format that supports 16 bits for
        //     the red channel.
        R16_UInt = 57,
        //
        // Сводка:
        //     A single-component, 16-bit signed-normalized-integer format that supports 16
        //     bits for the red channel.
        R16_SNorm = 58,
        //
        // Сводка:
        //     A single-component, 16-bit signed-integer format that supports 16 bits for the
        //     red channel.
        R16_SInt = 59,
        //
        // Сводка:
        //     A single-component, 8-bit typeless format that supports 8 bits for the red channel.
        R8_Typeless = 60,
        //
        // Сводка:
        //     A single-component, 8-bit unsigned-normalized-integer format that supports 8
        //     bits for the red channel.
        R8_UNorm = 61,
        //
        // Сводка:
        //     A single-component, 8-bit unsigned-integer format that supports 8 bits for the
        //     red channel.
        R8_UInt = 62,
        //
        // Сводка:
        //     A single-component, 8-bit signed-normalized-integer format that supports 8 bits
        //     for the red channel.
        R8_SNorm = 63,
        //
        // Сводка:
        //     A single-component, 8-bit signed-integer format that supports 8 bits for the
        //     red channel.
        R8_SInt = 64,
        //
        // Сводка:
        //     A single-component, 8-bit unsigned-normalized-integer format for alpha only.
        A8_UNorm = 65,
        //
        // Сводка:
        //     A single-component, 1-bit unsigned-normalized integer format that supports 1
        //     bit for the red channel. ?.
        R1_UNorm = 66,
        //
        // Сводка:
        //     Three partial-precision floating-point numbers encoded into a single 32-bit value
        //     all sharing the same 5-bit exponent (variant of s10e5, which is sign bit, 10-bit
        //     mantissa, and 5-bit biased (15) exponent). There is no sign bit, and there is
        //     a shared 5-bit biased (15) exponent and a 9-bit mantissa for each channel, as
        //     shown in the following illustration. 2,6,7.
        R9G9B9E5_Sharedexp = 67,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-normalized-integer format. This packed RGB
        //     format is analogous to the UYVY format. Each 32-bit block describes a pair of
        //     pixels: (R8, G8, B8) and (R8, G8, B8) where the R8/B8 values are repeated, and
        //     the G8 values are unique to each pixel. ? Width must be even.
        R8G8_B8G8_UNorm = 68,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-normalized-integer format. This packed RGB
        //     format is analogous to the YUY2 format. Each 32-bit block describes a pair of
        //     pixels: (R8, G8, B8) and (R8, G8, B8) where the R8/B8 values are repeated, and
        //     the G8 values are unique to each pixel. ? Width must be even.
        G8R8_G8B8_UNorm = 69,
        //
        // Сводка:
        //     Four-component typeless block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC1_Typeless = 70,
        //
        // Сводка:
        //     Four-component block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC1_UNorm = 71,
        //
        // Сводка:
        //     Four-component block-compression format for sRGB data. For information about
        //     block-compression formats, see Texture Block Compression in Direct3D 11.
        BC1_UNorm_SRgb = 72,
        //
        // Сводка:
        //     Four-component typeless block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC2_Typeless = 73,
        //
        // Сводка:
        //     Four-component block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC2_UNorm = 74,
        //
        // Сводка:
        //     Four-component block-compression format for sRGB data. For information about
        //     block-compression formats, see Texture Block Compression in Direct3D 11.
        BC2_UNorm_SRgb = 75,
        //
        // Сводка:
        //     Four-component typeless block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC3_Typeless = 76,
        //
        // Сводка:
        //     Four-component block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC3_UNorm = 77,
        //
        // Сводка:
        //     Four-component block-compression format for sRGB data. For information about
        //     block-compression formats, see Texture Block Compression in Direct3D 11.
        BC3_UNorm_SRgb = 78,
        //
        // Сводка:
        //     One-component typeless block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC4_Typeless = 79,
        //
        // Сводка:
        //     One-component block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC4_UNorm = 80,
        //
        // Сводка:
        //     One-component block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC4_SNorm = 81,
        //
        // Сводка:
        //     Two-component typeless block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC5_Typeless = 82,
        //
        // Сводка:
        //     Two-component block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC5_UNorm = 83,
        //
        // Сводка:
        //     Two-component block-compression format. For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC5_SNorm = 84,
        //
        // Сводка:
        //     A three-component, 16-bit unsigned-normalized-integer format that supports 5
        //     bits for blue, 6 bits for green, and 5 bits for red. Direct3D 10 through Direct3D
        //     11:??This value is defined for DXGI. However, Direct3D 10, 10.1, or 11 devices
        //     do not support this format. Direct3D 11.1:??This value is not supported until
        //     Windows?8.
        B5G6R5_UNorm = 85,
        //
        // Сводка:
        //     A four-component, 16-bit unsigned-normalized-integer format that supports 5 bits
        //     for each color channel and 1-bit alpha. Direct3D 10 through Direct3D 11:??This
        //     value is defined for DXGI. However, Direct3D 10, 10.1, or 11 devices do not support
        //     this format. Direct3D 11.1:??This value is not supported until Windows?8.
        B5G5R5A1_UNorm = 86,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-normalized-integer format that supports 8 bits
        //     for each color channel and 8-bit alpha.
        B8G8R8A8_UNorm = 87,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-normalized-integer format that supports 8 bits
        //     for each color channel and 8 bits unused.
        B8G8R8X8_UNorm = 88,
        //
        // Сводка:
        //     A four-component, 32-bit 2.8-biased fixed-point format that supports 10 bits
        //     for each color channel and 2-bit alpha.
        R10G10B10_Xr_Bias_A2_UNorm = 89,
        //
        // Сводка:
        //     A four-component, 32-bit typeless format that supports 8 bits for each channel
        //     including alpha. ?
        B8G8R8A8_Typeless = 90,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-normalized standard RGB format that supports
        //     8 bits for each channel including alpha. ?
        B8G8R8A8_UNorm_SRgb = 91,
        //
        // Сводка:
        //     A four-component, 32-bit typeless format that supports 8 bits for each color
        //     channel, and 8 bits are unused. ?
        B8G8R8X8_Typeless = 92,
        //
        // Сводка:
        //     A four-component, 32-bit unsigned-normalized standard RGB format that supports
        //     8 bits for each color channel, and 8 bits are unused. ?
        B8G8R8X8_UNorm_SRgb = 93,
        //
        // Сводка:
        //     A typeless block-compression format. ? For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC6H_Typeless = 94,
        //
        // Сводка:
        //     A block-compression format. ? For information about block-compression formats,
        //     see Texture Block Compression in Direct3D 11.?
        BC6H_Uf16 = 95,
        //
        // Сводка:
        //     A block-compression format. ? For information about block-compression formats,
        //     see Texture Block Compression in Direct3D 11.?
        BC6H_Sf16 = 96,
        //
        // Сводка:
        //     A typeless block-compression format. ? For information about block-compression
        //     formats, see Texture Block Compression in Direct3D 11.
        BC7_Typeless = 97,
        //
        // Сводка:
        //     A block-compression format. ? For information about block-compression formats,
        //     see Texture Block Compression in Direct3D 11.
        BC7_UNorm = 98,
        //
        // Сводка:
        //     A block-compression format. ? For information about block-compression formats,
        //     see Texture Block Compression in Direct3D 11.
        BC7_UNorm_SRgb = 99,
        //
        // Сводка:
        //     Most common YUV 4:4:4 video resource format. Valid view formats for this video
        //     resource format are SharpDX.DXGI.Format.R8G8B8A8_UNorm and SharpDX.DXGI.Format.R8G8B8A8_UInt.
        //     For UAVs, an additional valid view format is SharpDX.DXGI.Format.R32_UInt. By
        //     using SharpDX.DXGI.Format.R32_UInt for UAVs, you can both read and write as opposed
        //     to just write for SharpDX.DXGI.Format.R8G8B8A8_UNorm and SharpDX.DXGI.Format.R8G8B8A8_UInt.
        //     Supported view types are SRV, RTV, and UAV. One view provides a straightforward
        //     mapping of the entire surface. The mapping to the view channel is V->R8, U->G8,
        //     Y->B8, and A->A8. For more info about YUV formats for video rendering, see Recommended
        //     8-Bit YUV Formats for Video Rendering. Direct3D 11.1:??This value is not supported
        //     until Windows?8.
        AYUV = 100,
        //
        // Сводка:
        //     10-bit per channel packed YUV 4:4:4 video resource format. Valid view formats
        //     for this video resource format are SharpDX.DXGI.Format.R10G10B10A2_UNorm and
        //     SharpDX.DXGI.Format.R10G10B10A2_UInt. For UAVs, an additional valid view format
        //     is SharpDX.DXGI.Format.R32_UInt. By using SharpDX.DXGI.Format.R32_UInt for UAVs,
        //     you can both read and write as opposed to just write for SharpDX.DXGI.Format.R10G10B10A2_UNorm
        //     and SharpDX.DXGI.Format.R10G10B10A2_UInt. Supported view types are SRV and UAV.
        //     One view provides a straightforward mapping of the entire surface. The mapping
        //     to the view channel is U->R10, Y->G10, V->B10, and A->A2. For more info about
        //     YUV formats for video rendering, see Recommended 8-Bit YUV Formats for Video
        //     Rendering. Direct3D 11.1:??This value is not supported until Windows?8.
        Y410 = 101,
        //
        // Сводка:
        //     16-bit per channel packed YUV 4:4:4 video resource format. Valid view formats
        //     for this video resource format are SharpDX.DXGI.Format.R16G16B16A16_UNorm and
        //     SharpDX.DXGI.Format.R16G16B16A16_UInt. Supported view types are SRV and UAV.
        //     One view provides a straightforward mapping of the entire surface. The mapping
        //     to the view channel is U->R16, Y->G16, V->B16, and A->A16. For more info about
        //     YUV formats for video rendering, see Recommended 8-Bit YUV Formats for Video
        //     Rendering. Direct3D 11.1:??This value is not supported until Windows?8.
        Y416 = 102,
        //
        // Сводка:
        //     Most common YUV 4:2:0 video resource format. Valid luminance data view formats
        //     for this video resource format are SharpDX.DXGI.Format.R8_UNorm and SharpDX.DXGI.Format.R8_UInt.
        //     Valid chrominance data view formats (width and height are each 1/2 of luminance
        //     view) for this video resource format are SharpDX.DXGI.Format.R8G8_UNorm and SharpDX.DXGI.Format.R8G8_UInt.
        //     Supported view types are SRV, RTV, and UAV. For luminance data view, the mapping
        //     to the view channel is Y->R8. For chrominance data view, the mapping to the view
        //     channel is U->R8 and V->G8. For more info about YUV formats for video rendering,
        //     see Recommended 8-Bit YUV Formats for Video Rendering. Width and height must
        //     be even. Direct3D 11 staging resources and initData parameters for this format
        //     use (rowPitch * (height + (height / 2))) bytes. The first (SysMemPitch * height)
        //     bytes are the Y plane, the remaining (SysMemPitch * (height / 2)) bytes are the
        //     UV plane. An app using the YUY 4:2:0 formats must map the luma (Y) plane separately
        //     from the chroma (UV) planes. Developers do this by calling SharpDX.Direct3D12.Device.CreateShaderResourceView
        //     twice for the same texture and passing in 1-channel and 2-channel formats. Passing
        //     in a 1-channel format compatible with the Y plane maps only the Y plane. Passing
        //     in a 2-channel format compatible with the UV planes (together) maps only the
        //     U and V planes as a single resource view. Direct3D 11.1:??This value is not supported
        //     until Windows?8.
        NV12 = 103,
        //
        // Сводка:
        //     10-bit per channel planar YUV 4:2:0 video resource format. Valid luminance data
        //     view formats for this video resource format are SharpDX.DXGI.Format.R16_UNorm
        //     and SharpDX.DXGI.Format.R16_UInt. The runtime does not enforce whether the lowest
        //     6 bits are 0 (given that this video resource format is a 10-bit format that uses
        //     16 bits). If required, application shader code would have to enforce this manually.
        //     From the runtime's point of view, SharpDX.DXGI.Format.P010 is no different than
        //     SharpDX.DXGI.Format.P016. Valid chrominance data view formats (width and height
        //     are each 1/2 of luminance view) for this video resource format are SharpDX.DXGI.Format.R16G16_UNorm
        //     and SharpDX.DXGI.Format.R16G16_UInt. For UAVs, an additional valid chrominance
        //     data view format is SharpDX.DXGI.Format.R32_UInt. By using SharpDX.DXGI.Format.R32_UInt
        //     for UAVs, you can both read and write as opposed to just write for SharpDX.DXGI.Format.R16G16_UNorm
        //     and SharpDX.DXGI.Format.R16G16_UInt. Supported view types are SRV, RTV, and UAV.
        //     For luminance data view, the mapping to the view channel is Y->R16. For chrominance
        //     data view, the mapping to the view channel is U->R16 and V->G16. For more info
        //     about YUV formats for video rendering, see Recommended 8-Bit YUV Formats for
        //     Video Rendering. Width and height must be even. Direct3D 11 staging resources
        //     and initData parameters for this format use (rowPitch * (height + (height / 2)))
        //     bytes. The first (SysMemPitch * height) bytes are the Y plane, the remaining
        //     (SysMemPitch * (height / 2)) bytes are the UV plane. An app using the YUY 4:2:0
        //     formats must map the luma (Y) plane separately from the chroma (UV) planes. Developers
        //     do this by calling SharpDX.Direct3D12.Device.CreateShaderResourceView twice for
        //     the same texture and passing in 1-channel and 2-channel formats. Passing in a
        //     1-channel format compatible with the Y plane maps only the Y plane. Passing in
        //     a 2-channel format compatible with the UV planes (together) maps only the U and
        //     V planes as a single resource view. Direct3D 11.1:??This value is not supported
        //     until Windows?8.
        P010 = 104,
        //
        // Сводка:
        //     16-bit per channel planar YUV 4:2:0 video resource format. Valid luminance data
        //     view formats for this video resource format are SharpDX.DXGI.Format.R16_UNorm
        //     and SharpDX.DXGI.Format.R16_UInt. Valid chrominance data view formats (width
        //     and height are each 1/2 of luminance view) for this video resource format are
        //     SharpDX.DXGI.Format.R16G16_UNorm and SharpDX.DXGI.Format.R16G16_UInt. For UAVs,
        //     an additional valid chrominance data view format is SharpDX.DXGI.Format.R32_UInt.
        //     By using SharpDX.DXGI.Format.R32_UInt for UAVs, you can both read and write as
        //     opposed to just write for SharpDX.DXGI.Format.R16G16_UNorm and SharpDX.DXGI.Format.R16G16_UInt.
        //     Supported view types are SRV, RTV, and UAV. For luminance data view, the mapping
        //     to the view channel is Y->R16. For chrominance data view, the mapping to the
        //     view channel is U->R16 and V->G16. For more info about YUV formats for video
        //     rendering, see Recommended 8-Bit YUV Formats for Video Rendering. Width and height
        //     must be even. Direct3D 11 staging resources and initData parameters for this
        //     format use (rowPitch * (height + (height / 2))) bytes. The first (SysMemPitch
        //     * height) bytes are the Y plane, the remaining (SysMemPitch * (height / 2)) bytes
        //     are the UV plane. An app using the YUY 4:2:0 formats must map the luma (Y) plane
        //     separately from the chroma (UV) planes. Developers do this by calling SharpDX.Direct3D12.Device.CreateShaderResourceView
        //     twice for the same texture and passing in 1-channel and 2-channel formats. Passing
        //     in a 1-channel format compatible with the Y plane maps only the Y plane. Passing
        //     in a 2-channel format compatible with the UV planes (together) maps only the
        //     U and V planes as a single resource view. Direct3D 11.1:??This value is not supported
        //     until Windows?8.
        P016 = 105,
        //
        // Сводка:
        //     8-bit per channel planar YUV 4:2:0 video resource format. This format is subsampled
        //     where each pixel has its own Y value, but each 2x2 pixel block shares a single
        //     U and V value. The runtime requires that the width and height of all resources
        //     that are created with this format are multiples of 2. The runtime also requires
        //     that the left, right, top, and bottom members of any SharpDX.Mathematics.Interop.RawRectangle
        //     that are used for this format are multiples of 2. This format differs from SharpDX.DXGI.Format.NV12
        //     in that the layout of the data within the resource is completely opaque to applications.
        //     Applications cannot use the CPU to map the resource and then access the data
        //     within the resource. You cannot use shaders with this format. Because of this
        //     behavior, legacy hardware that supports a non-NV12 4:2:0 layout (for example,
        //     YV12, and so on) can be used. Also, new hardware that has a 4:2:0 implementation
        //     better than NV12 can be used when the application does not need the data to be
        //     in a standard layout. For more info about YUV formats for video rendering, see
        //     Recommended 8-Bit YUV Formats for Video Rendering. Width and height must be even.
        //     Direct3D 11 staging resources and initData parameters for this format use (rowPitch
        //     * (height + (height / 2))) bytes. An app using the YUY 4:2:0 formats must map
        //     the luma (Y) plane separately from the chroma (UV) planes. Developers do this
        //     by calling SharpDX.Direct3D12.Device.CreateShaderResourceView twice for the same
        //     texture and passing in 1-channel and 2-channel formats. Passing in a 1-channel
        //     format compatible with the Y plane maps only the Y plane. Passing in a 2-channel
        //     format compatible with the UV planes (together) maps only the U and V planes
        //     as a single resource view. Direct3D 11.1:??This value is not supported until
        //     Windows?8.
        Opaque420 = 106,
        //
        // Сводка:
        //     Most common YUV 4:2:2 video resource format. Valid view formats for this video
        //     resource format are SharpDX.DXGI.Format.R8G8B8A8_UNorm and SharpDX.DXGI.Format.R8G8B8A8_UInt.
        //     For UAVs, an additional valid view format is SharpDX.DXGI.Format.R32_UInt. By
        //     using SharpDX.DXGI.Format.R32_UInt for UAVs, you can both read and write as opposed
        //     to just write for SharpDX.DXGI.Format.R8G8B8A8_UNorm and SharpDX.DXGI.Format.R8G8B8A8_UInt.
        //     Supported view types are SRV and UAV. One view provides a straightforward mapping
        //     of the entire surface. The mapping to the view channel is Y0->R8, U0->G8, Y1->B8,
        //     and V0->A8. A unique valid view format for this video resource format is SharpDX.DXGI.Format.R8G8_B8G8_UNorm.
        //     With this view format, the width of the view appears to be twice what the SharpDX.DXGI.Format.R8G8B8A8_UNorm
        //     or SharpDX.DXGI.Format.R8G8B8A8_UInt view would be when hardware reconstructs
        //     RGBA automatically on read and before filtering. This Direct3D hardware behavior
        //     is legacy and is likely not useful any more. With this view format, the mapping
        //     to the view channel is Y0->R8, U0-> G8[0], Y1->B8, and V0-> G8[1]. For more info
        //     about YUV formats for video rendering, see Recommended 8-Bit YUV Formats for
        //     Video Rendering. Width must be even. Direct3D 11.1:??This value is not supported
        //     until Windows?8.
        YUY2 = 107,
        //
        // Сводка:
        //     10-bit per channel packed YUV 4:2:2 video resource format. Valid view formats
        //     for this video resource format are SharpDX.DXGI.Format.R16G16B16A16_UNorm and
        //     SharpDX.DXGI.Format.R16G16B16A16_UInt. The runtime does not enforce whether the
        //     lowest 6 bits are 0 (given that this video resource format is a 10-bit format
        //     that uses 16 bits). If required, application shader code would have to enforce
        //     this manually. From the runtime's point of view, SharpDX.DXGI.Format.Y210 is
        //     no different than SharpDX.DXGI.Format.Y216. Supported view types are SRV and
        //     UAV. One view provides a straightforward mapping of the entire surface. The mapping
        //     to the view channel is Y0->R16, U->G16, Y1->B16, and V->A16. For more info about
        //     YUV formats for video rendering, see Recommended 8-Bit YUV Formats for Video
        //     Rendering. Width must be even. Direct3D 11.1:??This value is not supported until
        //     Windows?8.
        Y210 = 108,
        //
        // Сводка:
        //     16-bit per channel packed YUV 4:2:2 video resource format. Valid view formats
        //     for this video resource format are SharpDX.DXGI.Format.R16G16B16A16_UNorm and
        //     SharpDX.DXGI.Format.R16G16B16A16_UInt. Supported view types are SRV and UAV.
        //     One view provides a straightforward mapping of the entire surface. The mapping
        //     to the view channel is Y0->R16, U->G16, Y1->B16, and V->A16. For more info about
        //     YUV formats for video rendering, see Recommended 8-Bit YUV Formats for Video
        //     Rendering. Width must be even. Direct3D 11.1:??This value is not supported until
        //     Windows?8.
        Y216 = 109,
        //
        // Сводка:
        //     Most common planar YUV 4:1:1 video resource format. Valid luminance data view
        //     formats for this video resource format are SharpDX.DXGI.Format.R8_UNorm and SharpDX.DXGI.Format.R8_UInt.
        //     Valid chrominance data view formats (width and height are each 1/4 of luminance
        //     view) for this video resource format are SharpDX.DXGI.Format.R8G8_UNorm and SharpDX.DXGI.Format.R8G8_UInt.
        //     Supported view types are SRV, RTV, and UAV. For luminance data view, the mapping
        //     to the view channel is Y->R8. For chrominance data view, the mapping to the view
        //     channel is U->R8 and V->G8. For more info about YUV formats for video rendering,
        //     see Recommended 8-Bit YUV Formats for Video Rendering. Width must be a multiple
        //     of 4. Direct3D11 staging resources and initData parameters for this format use
        //     (rowPitch * height * 2) bytes. The first (SysMemPitch * height) bytes are the
        //     Y plane, the next ((SysMemPitch / 2) * height) bytes are the UV plane, and the
        //     remainder is padding. Direct3D 11.1:??This value is not supported until Windows?8.
        NV11 = 110,
        //
        // Сводка:
        //     4-bit palletized YUV format that is commonly used for DVD subpicture. For more
        //     info about YUV formats for video rendering, see Recommended 8-Bit YUV Formats
        //     for Video Rendering. Direct3D 11.1:??This value is not supported until Windows?8.
        AI44 = 111,
        //
        // Сводка:
        //     4-bit palletized YUV format that is commonly used for DVD subpicture. For more
        //     info about YUV formats for video rendering, see Recommended 8-Bit YUV Formats
        //     for Video Rendering. Direct3D 11.1:??This value is not supported until Windows?8.
        IA44 = 112,
        //
        // Сводка:
        //     8-bit palletized format that is used for palletized RGB data when the processor
        //     processes ISDB-T data and for palletized YUV data when the processor processes
        //     BluRay data. For more info about YUV formats for video rendering, see Recommended
        //     8-Bit YUV Formats for Video Rendering. Direct3D 11.1:??This value is not supported
        //     until Windows?8.
        P8 = 113,
        //
        // Сводка:
        //     8-bit palletized format with 8 bits of alpha that is used for palletized YUV
        //     data when the processor processes BluRay data. For more info about YUV formats
        //     for video rendering, see Recommended 8-Bit YUV Formats for Video Rendering. Direct3D
        //     11.1:??This value is not supported until Windows?8.
        A8P8 = 114,
        //
        // Сводка:
        //     A four-component, 16-bit unsigned-normalized integer format that supports 4 bits
        //     for each channel including alpha. Direct3D 11.1:??This value is not supported
        //     until Windows?8.
        B4G4R4A4_UNorm = 115,
        //
        // Сводка:
        //     A video format; an 8-bit version of a hybrid planar 4:2:2 format.
        P208 = 130,
        //
        // Сводка:
        //     An 8 bit YCbCrA 4:4 rendering format.
        V208 = 131,
        //
        // Сводка:
        //     An 8 bit YCbCrA 4:4:4:4 rendering format.
        V408 = 132
    }
}
