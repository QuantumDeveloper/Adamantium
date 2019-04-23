using AdamantiumVulkan.Core;
using System;
using System.Runtime.InteropServices;

namespace Adamantium.Engine.Graphics
{
    /// <summary>
    /// PixelFormat is equivalent to <see cref="AdamantiumVulkan.Format"/>.
    /// </summary>
    /// <remarks>
    /// This structure is implicitly castable to and from <see cref="AdamantiumVulkan.Format"/>, you can use it inplace where <see cref="AdamantiumVulkan.Format"/> is required
    /// and vice-versa.
    /// Usage is slightly different from <see cref="AdamantiumVulkan.Format"/>, as you have to select the type of the pixel format first (<example>Typeless, SInt</example>...etc)
    /// and then access the available pixel formats for this type. Example: PixelFormat.UNorm.R8.
    /// </remarks>
    /// <msdn-id>bb173059</msdn-id>	
    /// <unmanaged>DXGI_FORMAT</unmanaged>	
    /// <unmanaged-short>DXGI_FORMAT</unmanaged-short>	
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    public struct SurfaceFormat : IEquatable<SurfaceFormat>
    {
        /// <summary>
        /// Gets the value as a <see cref="AdamantiumVulkan.Format"/> enum.
        /// </summary>
        public readonly Format Value;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="format"></param>
        private SurfaceFormat(Format format)
        {
            Value = format;
        }

        /// <summary>
        /// Size in bytes of current Surface format
        /// </summary>
        public int SizeInBytes => FormatHelper.SizeOfInBytes(this);

        /// <summary>
        /// Corresponds to <see cref="Format.Unknown"/>
        /// </summary>
        public static readonly SurfaceFormat Undefined = new SurfaceFormat(Format.UNDEFINED);

        /// <summary>
        /// <see cref="Format.R4G4_UNORM_PACK8"/> formats
        /// </summary>
        public static class R4G4
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R4G4_UNORM_PACK8"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R4G4_UNORM_PACK8);
        }

        /// <summary>
        /// <see cref="Format.R4G4B4A4_UNORM_PACK16"/> formats
        /// </summary>
        public static class R4G4B4A4
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R4G4B4A4_UNORM_PACK16"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R4G4B4A4_UNORM_PACK16);
        }

        /// <summary>
        /// <see cref="Format.B4G4R4A4_UNORM_PACK16"/> formats
        /// </summary>
        public static class B4G4R4A4
        {
            /// <summary>
            /// Corresponds to <see cref="Format.B4G4R4A4_UNORM_PACK16"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.B4G4R4A4_UNORM_PACK16);
        }

        /// <summary>
        /// <see cref="Format.R5G6B5_UNORM_PACK16"/> formats
        /// </summary>
        public static class R5G6B5
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R5G6B5_UNORM_PACK16"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R5G6B5_UNORM_PACK16);
        }

        /// <summary>
        /// <see cref="Format.B5G6R5_UNORM_PACK16"/> formats
        /// </summary>
        public static class B5G6R5
        {
            /// <summary>
            /// Corresponds to <see cref="Format.B5G6R5_UNORM_PACK16"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.B5G6R5_UNORM_PACK16);
        }

        /// <summary>
        /// <see cref="Format.R5G5B5A1_UNORM_PACK16"/> formats
        /// </summary>
        public static class R5G5B5A1
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R5G5B5A1_UNORM_PACK16"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R5G5B5A1_UNORM_PACK16);
        }

        /// <summary>
        /// <see cref="Format.B5G5R5A1_UNORM_PACK16"/> formats
        /// </summary>
        public static class B5G5R5A1
        {
            /// <summary>
            /// Corresponds to <see cref="Format.B5G5R5A1_UNORM_PACK16"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.B5G5R5A1_UNORM_PACK16);
        }

        /// <summary>
        /// <see cref="Format.A1R5G5B5_UNORM_PACK16"/> formats
        /// </summary>
        public static class A1R5G5B5
        {
            /// <summary>
            /// Corresponds to <see cref="Format.A1R5G5B5_UNORM_PACK16"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.A1R5G5B5_UNORM_PACK16);
        }

        /// <summary>
        /// <see cref="Format.R8"/> formats family
        /// </summary>
        public static class R8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R8_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R8_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R8_SNORM"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R8_SNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R8_USCALED"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.R8_USCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R8_SSCALED"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.R8_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R8_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R8_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R8_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R8_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R8_SRGB"/>
            /// </summary>
            public static readonly SurfaceFormat Srgb = new SurfaceFormat(Format.R8_SRGB);
        }

        /// <summary>
        /// <see cref="Format.R8G8"/> formats family
        /// </summary>
        public static class R8G8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R8G8_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R8G8_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8_SNORM"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R8G8_SNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8_USCALED"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.R8G8_USCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8_SSCALED"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.R8G8_SSCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R8G8_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R8G8_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8_SRGB"/>
            /// </summary>
            public static readonly SurfaceFormat Srgb = new SurfaceFormat(Format.R8G8_SRGB);
        }

        /// <summary>
        /// <see cref="Format.R8G8B8"/> formats family
        /// </summary>
        public static class R8G8B8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R8G8B8_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8_SNORM"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R8G8B8_SNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8_USCALED"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.R8G8B8_USCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8_SSCALED"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.R8G8B8_SSCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R8G8B8_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R8G8B8_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8_SRGB"/>
            /// </summary>
            public static readonly SurfaceFormat Srgb = new SurfaceFormat(Format.R8G8B8_SRGB);

        }

        /// <summary>
        /// <see cref="Format.B8G8R8"/> formats family
        /// </summary>
        public static class B8G8R8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.B8G8R8_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8_SNORM"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.B8G8R8_SNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8_USCALED"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.B8G8R8_USCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8_SSCALED"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.B8G8R8_SSCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.B8G8R8_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.B8G8R8_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8_SRGB"/>
            /// </summary>
            public static readonly SurfaceFormat Srgb = new SurfaceFormat(Format.B8G8R8_SRGB);

        }

        /// <summary>
        /// <see cref="Format.R8G8B8A8"/> formats family
        /// </summary>
        public static class R8G8B8A8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8A8_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R8G8B8A8_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8A8_SNORM"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R8G8B8A8_SNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8A8_USCALED"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.R8G8B8A8_USCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8A8_SSCALED"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.R8G8B8A8_SSCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8A8_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R8G8B8A8_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8A8_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R8G8B8A8_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R8G8B8A8_SRGB"/>
            /// </summary>
            public static readonly SurfaceFormat Srgb = new SurfaceFormat(Format.R8G8B8A8_SRGB);

        }

        /// <summary>
        /// <see cref="Format.B8G8R8A8"/> formats family
        /// </summary>
        public static class B8G8R8A8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8A8_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.B8G8R8A8_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8A8_SNORM"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.B8G8R8A8_SNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8A8_USCALED"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.B8G8R8A8_USCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8A8_SSCALED"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.B8G8R8A8_SSCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8A8_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.B8G8R8A8_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8A8_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.B8G8R8A8_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.B8G8R8A8_SRGB"/>
            /// </summary>
            public static readonly SurfaceFormat Srgb = new SurfaceFormat(Format.B8G8R8A8_SRGB);

        }

        /// <summary>
        /// <see cref="Format.A8B8G8R8"/> formats family
        /// </summary>
        public static class A8B8G8R8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.A8B8G8R8_UNORM_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.A8B8G8R8_UNORM_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A8B8G8R8_SNORM_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.A8B8G8R8_SNORM_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A8B8G8R8_USCALED_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.A8B8G8R8_USCALED_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A8B8G8R8_SSCALED_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.A8B8G8R8_SSCALED_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A8B8G8R8_UINT_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.A8B8G8R8_UINT_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A8B8G8R8_SINT_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.A8B8G8R8_SINT_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A8B8G8R8_SRGB_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat Srgb = new SurfaceFormat(Format.A8B8G8R8_SRGB_PACK32);

        }

        /// <summary>
        /// <see cref="Format.A2R10G10B10"/> formats family
        /// </summary>
        public static class A2R10G10B10
        {
            /// <summary>
            /// Corresponds to <see cref="Format.A2R10G10B10_UNORM_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.A2R10G10B10_UNORM_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A2R10G10B10_SNORM_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.A2R10G10B10_SNORM_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A2R10G10B10_USCALED_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.A2R10G10B10_USCALED_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A2R10G10B10_SSCALED_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.A2R10G10B10_SSCALED_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A2R10G10B10_UINT_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.A2R10G10B10_UINT_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A2R10G10B10_SINT_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.A2R10G10B10_SINT_PACK32);

        }

        /// <summary>
        /// <see cref="Format.A2B10G10R10"/> formats family
        /// </summary>
        public static class A2B10G10R10
        {
            /// <summary>
            /// Corresponds to <see cref="Format.A2B10G10R10_UNORM_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.A2B10G10R10_UNORM_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A2B10G10R10_SNORM_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.A2B10G10R10_SNORM_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A2B10G10R10_USCALED_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.A2B10G10R10_USCALED_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A2B10G10R10_SSCALED_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.A2B10G10R10_SSCALED_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A2B10G10R10_UINT_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.A2B10G10R10_UINT_PACK32);

            /// <summary>
            /// Corresponds to <see cref="Format.A2B10G10R10_SINT_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.A2B10G10R10_SINT_PACK32);

        }

        /// <summary>
        /// <see cref="Format.R16"/> formats family
        /// </summary>
        public static class R16
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R16_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R16_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R16_SNORM"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R16_SNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R16_USCALED"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.R16_USCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R16_SSCALED"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.R16_SSCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R16_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R16_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R16_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R16_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R16_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R16_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R16G16"/> formats family
        /// </summary>
        public static class R16G16
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R16G16_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R16G16_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16_SNORM"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R16G16_SNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16_USCALED"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.R16G16_USCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16_SSCALED"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.R16G16_SSCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R16G16_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R16G16_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R16G16_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R16G16B16"/> formats family
        /// </summary>
        public static class R16G16B16
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R16G16B16_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16_SNORM"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R16G16B16_SNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16_USCALED"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.R16G16B16_USCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16_SSCALED"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.R16G16B16_SSCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R16G16B16_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R16G16B16_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R16G16B16_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R16G16B16A16"/> formats family
        /// </summary>
        public static class R16G16B16A16
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16A16_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R16G16B16A16_UNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16A16_SNORM"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.R16G16B16A16_SNORM);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16A16_USCALED"/>
            /// </summary>
            public static readonly SurfaceFormat UScaled = new SurfaceFormat(Format.R16G16B16A16_USCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16A16_SSCALED"/>
            /// </summary>
            public static readonly SurfaceFormat SScaled = new SurfaceFormat(Format.R16G16B16A16_SSCALED);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16A16_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R16G16B16A16_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16A16_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R16G16B16A16_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R16G16B16A16_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R16G16B16A16_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R32"/> formats family
        /// </summary>
        public static class R32
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R32_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R32_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R32_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R32_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R32_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R32_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R32G32"/> formats family
        /// </summary>
        public static class R32G32
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R32G32_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R32G32_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R32G32_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R32G32_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R32G32_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R32G32_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R32G32B32"/> formats family
        /// </summary>
        public static class R32G32B32
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R32G32B32_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R32G32B32_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R32G32B32_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R32G32B32_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R32G32B32_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R32G32B32_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R32G32B32A32"/> formats family
        /// </summary>
        public static class R32G32B32A32
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R32G32B32A32_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R32G32B32A32_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R32G32B32A32_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R32G32B32A32_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R32G32B32A32_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R32G32B32A32_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R64"/> formats family
        /// </summary>
        public static class R64
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R64_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R64_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R64_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R64_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R64_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R64_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R64G64"/> formats family
        /// </summary>
        public static class R64G64
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R64G64_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R64G64_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R64G64_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R64G64_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R64G64_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R64G64_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R64G64B64"/> formats family
        /// </summary>
        public static class R64G64B64
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R64G64B64_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R64G64B64_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R64G64B64_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R64G64B64_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R64G64B64_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R64G64B64_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.R64G64B64A64"/> formats family
        /// </summary>
        public static class R64G64B64A64
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R64G64B64A64_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R64G64B64A64_UINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R64G64B64A64_SINT"/>
            /// </summary>
            public static readonly SurfaceFormat SInt = new SurfaceFormat(Format.R64G64B64A64_SINT);

            /// <summary>
            /// Corresponds to <see cref="Format.R64G64B64A64_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.R64G64B64A64_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.B10G11R11"/> formats family
        /// </summary>
        public static class B10G11R11
        {
            /// <summary>
            /// Corresponds to <see cref="Format.B10G11R11_UFLOAT_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UFloat = new SurfaceFormat(Format.B10G11R11_UFLOAT_PACK32);
        }

        /// <summary>
        /// <see cref="Format.E5B9G9R9"/> formats family
        /// </summary>
        public static class E5B9G9R9
        {
            /// <summary>
            /// Corresponds to <see cref="Format.E5B9G9R9_UFLOAT_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UFloat = new SurfaceFormat(Format.E5B9G9R9_UFLOAT_PACK32);
        }

        /// <summary>
        /// <see cref="Format.D16_UNORM"/> format
        /// </summary>
        public static class D16
        {
            /// <summary>
            /// Corresponds to <see cref="Format.D16_UNORM"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.D16_UNORM);
        }

        /// <summary>
        /// <see cref="Format.X8_D24_UNORM_PACK32"/> format
        /// </summary>
        public static class X8D24
        {
            /// <summary>
            /// Corresponds to <see cref="Format.X8_D24_UNORM_PACK32"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.X8_D24_UNORM_PACK32);
        }

        /// <summary>
        /// <see cref="Format.D32_SFLOAT"/> format
        /// </summary>
        public static class D32
        {
            /// <summary>
            /// Corresponds to <see cref="Format.D32_SFLOAT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloat = new SurfaceFormat(Format.D32_SFLOAT);
        }

        /// <summary>
        /// <see cref="Format.S8_UINT"/> format
        /// </summary>
        public static class S8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.S8_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.S8_UINT);
        }

        /// <summary>
        /// <see cref="Format.D16_UNORM_S8_UINT"/> format
        /// </summary>
        public static class D16S8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.D16_UNORM_S8_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UNormUInt = new SurfaceFormat(Format.D16_UNORM_S8_UINT);
        }

        /// <summary>
        /// <see cref="Format.D24_UNORM_S8_UINT"/> format
        /// </summary>
        public static class D24S8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.D24_UNORM_S8_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat UNormUInt = new SurfaceFormat(Format.D24_UNORM_S8_UINT);
        }

        /// <summary>
        /// <see cref="Format.D32_SFLOAT_S8_UINT"/> format
        /// </summary>
        public static class D32S8
        {
            /// <summary>
            /// Corresponds to <see cref="Format.D32_SFLOAT_S8_UINT"/>
            /// </summary>
            public static readonly SurfaceFormat SFloatUInt = new SurfaceFormat(Format.D32_SFLOAT_S8_UINT);
        }

        /// <summary>
        /// <see cref="Format.BC1"/> formats family
        /// </summary>
        public static class BC1
        {
            /// <summary>
            /// Corresponds to <see cref="Format.BC1_Typeless"/>
            /// </summary>
            public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC1_Typeless);

            /// <summary>
            /// Corresponds to <see cref="Format.BC1_UNorm"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC1_UNorm);

            /// <summary>
            /// Corresponds to <see cref="Format.BC1_UNorm_SRgb"/>
            /// </summary>
            public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.BC1_UNorm_SRgb);

        }

        /// <summary>
        /// DXGI.Format.BC2 formats
        /// </summary>
        public static class BC2
        {
            /// <summary>
            /// Corresponds to <see cref="Format.BC2_Typeless"/>
            /// </summary>
            public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC2_Typeless);

            /// <summary>
            /// Corresponds to <see cref="Format.BC2_UNorm"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC2_UNorm);

            /// <summary>
            /// Corresponds to <see cref="Format.BC2_UNorm_SRgb"/>
            /// </summary>
            public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.BC2_UNorm_SRgb);

        }

        /// <summary>
        /// DXGI.Format.BC3 formats
        /// </summary>
        public static class BC3
        {
            /// <summary>
            /// Corresponds to <see cref="Format.BC3_Typeless"/>
            /// </summary>
            public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC3_Typeless);

            /// <summary>
            /// Corresponds to <see cref="Format.BC3_UNorm"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC3_UNorm);

            /// <summary>
            /// Corresponds to <see cref="Format.BC3_UNorm_SRgb"/>
            /// </summary>
            public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.BC3_UNorm_SRgb);

        }

        /// <summary>
        /// DXGI.Format.BC4 formats
        /// </summary>
        public static class BC4
        {
            /// <summary>
            /// Corresponds to <see cref="Format.BC4_SNorm"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.BC4_SNorm);

            /// <summary>
            /// Corresponds to <see cref="Format.BC4_Typeless"/>
            /// </summary>
            public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC4_Typeless);

            /// <summary>
            /// Corresponds to <see cref="Format.BC4_UNorm"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC4_UNorm);

        }

        /// <summary>
        /// DXGI.Format.BC5 formats
        /// </summary>
        public static class BC5
        {
            /// <summary>
            /// Corresponds to <see cref="Format.BC5_SNorm"/>
            /// </summary>
            public static readonly SurfaceFormat SNorm = new SurfaceFormat(Format.BC5_SNorm);

            /// <summary>
            /// Corresponds to <see cref="Format.BC5_Typeless"/>
            /// </summary>
            public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC5_Typeless);

            /// <summary>
            /// Corresponds to <see cref="Format.BC5_UNorm"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC5_UNorm);

        }

        /// <summary>
        /// DXGI.Format.BC6H formats
        /// </summary>
        public static class BC6H
        {
            /// <summary>
            /// Corresponds to <see cref="Format.BC6H_Typeless"/>
            /// </summary>
            public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC6H_Typeless);

        }

        /// <summary>
        /// DXGI.Format.BC7 formats
        /// </summary>
        public static class BC7
        {
            /// <summary>
            /// Corresponds to <see cref="Format.BC7_Typeless"/>
            /// </summary>
            public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.BC7_Typeless);

            /// <summary>
            /// Corresponds to <see cref="Format.BC7_UNorm"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.BC7_UNorm);

            /// <summary>
            /// Corresponds to <see cref="Format.BC7_UNorm_SRgb"/>
            /// </summary>
            public static readonly SurfaceFormat UNormSRgb = new SurfaceFormat(Format.BC7_UNorm_SRgb);

        }

        /// <summary>
        /// DXGI.Format.R10G10B10A2 formats
        /// </summary>
        public static class R10G10B10A2
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R10G10B10A2_Typeless"/>
            /// </summary>
            public static readonly SurfaceFormat Typeless = new SurfaceFormat(Format.R10G10B10A2_Typeless);

            /// <summary>
            /// Corresponds to <see cref="Format.R10G10B10A2_UInt"/>
            /// </summary>
            public static readonly SurfaceFormat UInt = new SurfaceFormat(Format.R10G10B10A2_UInt);

            /// <summary>
            /// Corresponds to <see cref="Format.R10G10B10A2_UNorm"/>
            /// </summary>
            public static readonly SurfaceFormat UNorm = new SurfaceFormat(Format.R10G10B10A2_UNorm);

        }

        /// <summary>
        /// DXGI.Format.R11G11B10 formats
        /// </summary>
        public static class R11G11B10
        {
            /// <summary>
            /// Corresponds to <see cref="Format.R11G11B10_Float"/>
            /// </summary>
            public static readonly SurfaceFormat Float = new SurfaceFormat(Format.R11G11B10_Float);

        }

        /// <summary>
        /// Returns <see cref="Format"/> from <see cref="SurfaceFormat"/>
        /// </summary>
        /// <param name="from"></param>
        /// <returns></returns>
        public static implicit operator Format(SurfaceFormat from)
        {
            return from.Value;
        }

        /// <summary>
        /// Returns <see cref="SurfaceFormat"/> form <see cref="Format"/>
        /// </summary>
        /// <param name="from">DXGI Format</param>
        /// <returns></returns>
        public static implicit operator SurfaceFormat(Format from)
        {
            return new SurfaceFormat(from);
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <returns>
        /// true if the current object is equal to the <paramref name="other"/> parameter; otherwise, false.
        /// </returns>
        /// <param name="other"><see cref="SurfaceFormat"/> to compare</param>
        public bool Equals(SurfaceFormat other)
        {
            return Value == other.Value;
        }

        /// <summary>
        /// Indicates whether this instance and a specified object are equal.
        /// </summary>
        /// <returns>
        /// true if <paramref name="obj"/> and this instance are the same type and represent the same value; otherwise, false. 
        /// </returns>
        /// <param name="obj">The object to compare with the current instance. </param><filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is SurfaceFormat && Equals((SurfaceFormat)obj);
        }

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <returns>
        /// A 32-bit signed integer that is the hash code for this instance.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        /// <summary>
        /// Overload for comparing 2 <see cref="SurfaceFormat"/> variables
        /// </summary>
        /// <param name="left">left <see cref="SurfaceFormat"/></param>
        /// <param name="right">right <see cref="SurfaceFormat"/></param>
        /// <returns></returns>
        public static bool operator ==(SurfaceFormat left, SurfaceFormat right)
        {
            return left.Equals(right);
        }


        /// <summary>
        /// Overload for comparing 2 <see cref="SurfaceFormat"/> variables
        /// </summary>
        /// <param name="left">left <see cref="SurfaceFormat"/></param>
        /// <param name="right">right <see cref="SurfaceFormat"/></param>
        /// <returns></returns>
        public static bool operator !=(SurfaceFormat left, SurfaceFormat right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Returns the fully qualified type name of this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> containing a <see cref="SurfaceFormat"/>.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            return $"{Value}";
        }
    }
}
