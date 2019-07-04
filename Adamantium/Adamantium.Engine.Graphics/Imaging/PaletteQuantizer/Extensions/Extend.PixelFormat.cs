using AdamantiumVulkan.Core;
using System;

namespace SimplePaletteQuantizer.Extensions
{
    /// <summary>
    /// The utility extender class.
    /// </summary>
    public static partial class Extend
    {
        /// <summary>
        /// Gets the friendly name of the pixel format.
        /// </summary>
        /// <param name="pixelFormat">The pixel format.</param>
        /// <returns></returns>
        //public static String GetFriendlyName(this Format pixelFormat)
        //{
        //    switch (pixelFormat)
        //    {
        //        case PixelFormat.Format1bppIndexed:
        //            return "Indexed (2 colors)";

        //        case PixelFormat.Format4bppIndexed:
        //            return "Indexed (16 colors)";

        //        case PixelFormat.Format8bppIndexed:
        //            return "Indexed (256 colors)";

        //        case PixelFormat.Format16bppGrayScale:
        //            return "Grayscale (65536 shades)";

        //        case PixelFormat.Format16bppArgb1555:
        //            return "Highcolor + Alpha mask (32768 colors)";

        //        case PixelFormat.Format16bppRgb555:
        //        case PixelFormat.Format16bppRgb565:
        //            return "Highcolor (65536 colors)";

        //        case PixelFormat.Format24bppRgb:
        //            return "Truecolor (24-bit)";

        //        case PixelFormat.Format32bppArgb:
        //        case PixelFormat.Format32bppPArgb:
        //            return "Truecolor + Alpha (32-bit)";

        //        case PixelFormat.Format32bppRgb:
        //            return "Truecolor (32-bit)";

        //        case PixelFormat.Format48bppRgb:
        //            return "Truecolor (48-bit)";

        //        case PixelFormat.Format64bppArgb:
        //        case PixelFormat.Format64bppPArgb:
        //            return "Truecolor + Alpha (64-bit)";

        //        default:
        //            String message = string.Format("A pixel format '{0}' not supported!", pixelFormat);
        //            throw new NotSupportedException(message);
        //    }
        //}

        /// <summary>
        /// Determines whether the specified pixel format is supported.
        /// </summary>
        /// <param name="pixelFormat">The pixel format.</param>
        /// <returns>
        /// 	<c>true</c> if the specified pixel format is supported; otherwise, <c>false</c>.
        /// </returns>
        public static Boolean IsSupported(this Format pixelFormat)
        {
            var bitSize = pixelFormat.SizeOfInBits();
            if (bitSize <= 64)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the format by color count.
        /// </summary>
        public static Format GetFormatByColorCount(Int32 colorCount)
        {
            if (colorCount <= 0 || colorCount > 256)
            {
                String message = string.Format("A color count '{0}' not supported!", colorCount);
                throw new NotSupportedException(message);
            }

            return Format.R8G8B8_UNORM;
        }

        /// <summary>
        /// Determines whether the specified pixel format has an alpha channel.
        /// </summary>
        /// <param name="pixelFormat">The pixel format.</param>
        /// <returns>
        /// 	<c>true</c> if the specified pixel format has an alpha channel; otherwise, <c>false</c>.
        /// </returns>
        public static Boolean HasAlpha(this Format pixelFormat)
        {
            switch (pixelFormat)
            {
                case Format.R8G8B8A8_UNORM:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Determines whether [is deep color] [the specified pixel format].
        /// </summary>
        /// <param name="pixelFormat">The pixel format.</param>
        /// <returns>
        /// 	<c>true</c> if [is deep color] [the specified pixel format]; otherwise, <c>false</c>.
        /// </returns>
        public static Boolean IsDeepColor(this Format pixelFormat)
        {
            var bitSize = pixelFormat.SizeOfInBits();
            if (bitSize == 16 || bitSize >=48)
            {
                return true;
            }

            return false;
        }
    }
}


