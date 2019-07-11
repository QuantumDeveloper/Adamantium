using System.Runtime.InteropServices;
using Adamantium.Core;

namespace Adamantium.Imaging.Dds
{
    /// <summary>
    /// Internal structure used to describe a DDS pixel format.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PixelFormat
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PixelFormat" /> struct.
        /// </summary>
        /// <param name="flags">The flags.</param>
        /// <param name="fourCC">The four CC.</param>
        /// <param name="rgbBitCount">The RGB bit count.</param>
        /// <param name="rBitMask">The r bit mask.</param>
        /// <param name="gBitMask">The g bit mask.</param>
        /// <param name="bBitMask">The b bit mask.</param>
        /// <param name="aBitMask">A bit mask.</param>
        public PixelFormat(PixelFormatFlags flags, int fourCC, int rgbBitCount, uint rBitMask, uint gBitMask, uint bBitMask, uint aBitMask)
        {
            Size = Utilities.SizeOf<PixelFormat>();
            Flags = flags;
            FourCC = fourCC;
            RGBBitCount = rgbBitCount;
            RBitMask = rBitMask;
            GBitMask = gBitMask;
            BBitMask = bBitMask;
            ABitMask = aBitMask;
        }

        public int Size;
        public PixelFormatFlags Flags;
        public int FourCC;
        public int RGBBitCount;
        public uint RBitMask;
        public uint GBitMask;
        public uint BBitMask;
        public uint ABitMask;

        public static readonly PixelFormat DXT1 = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('D', 'X', 'T', '1'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat DXT2 = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('D', 'X', 'T', '2'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat DXT3 = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('D', 'X', 'T', '3'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat DXT4 = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('D', 'X', 'T', '4'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat DXT5 = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('D', 'X', 'T', '5'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat BC4_UNorm = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('B', 'C', '4', 'U'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat BC4_SNorm = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('B', 'C', '4', 'S'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat BC5_UNorm = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('B', 'C', '5', 'U'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat BC5_SNorm = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('B', 'C', '5', 'S'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat R8G8_B8G8 = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('R', 'G', 'B', 'G'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat G8R8_G8B8 = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('G', 'R', 'G', 'B'), 0, 0, 0, 0, 0);

        public static readonly PixelFormat A8R8G8B8 = new PixelFormat(PixelFormatFlags.Rgba, 0, 32, 0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000);

        public static readonly PixelFormat X8R8G8B8 = new PixelFormat(PixelFormatFlags.Rgb, 0, 32, 0x00ff0000, 0x0000ff00, 0x000000ff, 0x00000000);

        public static readonly PixelFormat A8B8G8R8 = new PixelFormat(PixelFormatFlags.Rgba, 0, 32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0xff000000);

        public static readonly PixelFormat X8B8G8R8 = new PixelFormat(PixelFormatFlags.Rgb, 0, 32, 0x000000ff, 0x0000ff00, 0x00ff0000, 0x00000000);

        public static readonly PixelFormat G16R16 = new PixelFormat(PixelFormatFlags.Rgb, 0, 32, 0x0000ffff, 0xffff0000, 0x00000000, 0x00000000);

        public static readonly PixelFormat R5G6B5 = new PixelFormat(PixelFormatFlags.Rgb, 0, 16, 0x0000f800, 0x000007e0, 0x0000001f, 0x00000000);

        public static readonly PixelFormat A1R5G5B5 = new PixelFormat(PixelFormatFlags.Rgba, 0, 16, 0x00007c00, 0x000003e0, 0x0000001f, 0x00008000);

        public static readonly PixelFormat A4R4G4B4 = new PixelFormat(PixelFormatFlags.Rgba, 0, 16, 0x00000f00, 0x000000f0, 0x0000000f, 0x0000f000);

        public static readonly PixelFormat R8G8B8 = new PixelFormat(PixelFormatFlags.Rgb, 0, 24, 0x00ff0000, 0x0000ff00, 0x000000ff, 0x00000000);

        public static readonly PixelFormat L8 = new PixelFormat(PixelFormatFlags.Luminance, 0, 8, 0xff, 0x00, 0x00, 0x00);

        public static readonly PixelFormat L16 = new PixelFormat(PixelFormatFlags.Luminance, 0, 16, 0xffff, 0x0000, 0x0000, 0x0000);

        public static readonly PixelFormat A8L8 = new PixelFormat(PixelFormatFlags.LuminanceAlpha, 0, 16, 0x00ff, 0x0000, 0x0000, 0xff00);

        public static readonly PixelFormat A8 = new PixelFormat(PixelFormatFlags.Alpha, 0, 8, 0x00, 0x00, 0x00, 0xff);

        public static readonly PixelFormat DX10 = new PixelFormat(PixelFormatFlags.FourCC, new FourCC('D', 'X', '1', '0'), 0, 0, 0, 0, 0);
    }
}