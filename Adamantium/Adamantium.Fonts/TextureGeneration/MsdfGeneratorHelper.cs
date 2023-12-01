using Adamantium.Mathematics;
using System;

namespace Adamantium.Fonts.TextureGeneration
{
    public static class MsdfGeneratorHelper
    {
        public static double Median(double a, double b, double c)
        {
            return Math.Max(Math.Min(a, b), Math.Min(Math.Max(a, b), c));
        }

        public static double GetRange(double pxRange, double scaleX, double scaleY)
        {
            return pxRange / Math.Min(scaleX, scaleY);
        }

        public static byte PixelFloatToByte(double x)
        {
            return (byte)Clamp(256.0 * x, 255.0);
        }

        public static double Clamp(double n, double b)
        {
            var tmp = n > 0 ? 1.0 : 0.0;

            return n >= 0 && n <= b ? n : tmp * b;
        }

        public static Color ApplyColorMask(Color color, bool redMask, bool greenMask, bool blueMask)
        {
            color.R *= redMask ? (byte)1 : (byte)0;
            color.G *= greenMask ? (byte)1 : (byte)0;
            color.B *= blueMask ? (byte)1 : (byte)0;

            return color;
        }
    }
}
