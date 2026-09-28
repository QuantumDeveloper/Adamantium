/// Copyright (c) 2008 Jeffrey Powers for Fluxcapacity Open Source.
/// Under the MIT License, details: License.txt.

namespace Adamantium.Imaging.Jpeg
{
    internal class YCbCr
    {
        // Not possible to convert without loss of colors YCbCr->RGB->YCbCr.
        // For details see https://stackoverflow.com/questions/30081784/jpeg-ycrcb-rgb-conversion-precision

        // ---- YCbCr -> RGB by TABLE, the way libjpeg does it ------------------------------------------------------
        // The conversion is three multiply-adds per pixel, and a photograph has millions of them - so the products are
        // precomputed for all 256 possible inputs and the per-pixel work becomes four lookups and two adds, with no
        // floating point at all. The green channel needs two contributions summed at higher precision, so those two
        // tables hold fixed-point values and are shifted down together.
        private const int FixedBits = 16;
        private const int Half = 1 << (FixedBits - 1);

        private static readonly int[] CrToR = new int[256];
        private static readonly int[] CbToB = new int[256];
        private static readonly int[] CrToG = new int[256];
        private static readonly int[] CbToG = new int[256];

        static YCbCr()
        {
            for (int i = 0; i < 256; i++)
            {
                var shifted = i - 128;
                CrToR[i] = (int)(1.402 * shifted + 0.5);
                CbToB[i] = (int)(1.772 * shifted + 0.5);
                CrToG[i] = (int)(-0.71414 * (1 << FixedBits) * shifted);
                CbToG[i] = (int)(-0.34414 * (1 << FixedBits) * shifted) + Half;
            }
        }

        private static byte Clamp(int value) => value < 0 ? (byte)0 : value > 255 ? (byte)255 : (byte)value;

        /// <summary>One pixel from YCbCr to RGB through the tables above. Same arithmetic as <see cref="toRGB"/>, which
        /// it replaced in the decode path; results may differ by one where the old code truncated and this rounds.</summary>
        public static void ToRgbFast(ref byte y, ref byte cb, ref byte cr)
        {
            int luma = y;
            int b = cb;
            int r = cr;

            var red = luma + CrToR[r];
            var green = luma + ((CbToG[b] + CrToG[r]) >> FixedBits);
            var blue = luma + CbToB[b];

            y = Clamp(red);
            cb = Clamp(green);
            cr = Clamp(blue);
        }

        public static void toRGB(ref byte c1, ref byte c2, ref byte c3)
        {
            double dY = c1;
            double dCb2 = (double)c2 - 128;
            double dCr2 = (double)c3 - 128;

            double dR = dY + 1.402 * dCr2;
            double dG = dY - 0.34414 * dCb2 - 0.71414 * dCr2;
            double dB = dY + 1.772 * dCb2;

            c1 = dR > 255 ? (byte)255 : dR < 0 ? (byte)0 : (byte)dR;
            c2 = dG > 255 ? (byte)255 : dG < 0 ? (byte)0 : (byte)dG;
            c3 = dB > 255 ? (byte)255 : dB < 0 ? (byte)0 : (byte)dB;

        }

        public static void fromRGB(ref byte c1, ref byte c2, ref byte c3)
        {
            double dR = c1;
            double dG = c2;
            double dB = c3;

            c1 = (byte)(0.299 * dR + 0.587 * dG + 0.114 * dB);
            c2 = (byte)(128 + (byte)(-0.168736 * dR - 0.331264 * dG + 0.5 * dB));
            c3 = (byte)(128 + (byte)(0.5 * dR - 0.418688 * dG - 0.081312 * dB));
        }

        ///* RGB to YCbCr range 0-255 */
        //public static void fromRGB(byte[] rgb, byte[] ycbcr)
        //{
        //    ycbcr[0] = (byte)((0.299 * (float)rgb[0] + 0.587 * (float)rgb[1] + 0.114 * (float)rgb[2]));
        //    ycbcr[1] = (byte)(128 + (byte)((-0.16874 * (float)rgb[0] - 0.33126 * (float)rgb[1] + 0.5 * (float)rgb[2])));
        //    ycbcr[2] = (byte)(128 + (byte)((0.5 * (float)rgb[0] - 0.41869 * (float)rgb[1] - 0.08131 * (float)rgb[2])));
        //}


        /* RGB to YCbCr range 0-255 */
        public static float[] fromRGB(float[] data)
        {
            float[] dest = new float[3];

            dest[0] = (float)(0.299 * data[0] + 0.587 * data[1] + 0.114 * data[2]);
            dest[1] = 128 + (float)(-0.16874 * data[0] - 0.33126 * data[1] + 0.5 * data[2]);
            dest[2] = 128 + (float)(0.5 * data[0] - 0.41869 * data[1] - 0.08131 * data[2]);

            return dest;
        }
    }

}