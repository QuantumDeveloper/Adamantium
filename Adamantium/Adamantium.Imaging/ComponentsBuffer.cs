using System;
using Adamantium.Core;
using Adamantium.Imaging.Jpeg;

namespace Adamantium.Imaging
{
    public class ComponentsBuffer
    {
        public byte[][,] Raster { get; private set; }

        public int ComponentCount => Raster.Length;

        public int Width { get; private set; }

        public int Height { get; private set; }

        public ColorModel ColorModel { get; private set; }

        /// <summary> X density (dots per inch).</summary>
        public double DensityX { get; set; }
        /// <summary> Y density (dots per inch).</summary>
        public double DensityY { get; set; }

        public ComponentsBuffer(ColorModel cm, PixelBuffer pixelBuffer): 
            this(cm, pixelBuffer.GetComponents())
        {
        }

        public ComponentsBuffer(ColorModel cm, byte[][,] raster)
        {
            Width = raster[0].GetLength(0);
            Height = raster[0].GetLength(1);
            Raster = raster;
            ColorModel = cm;
        }

        /// <summary>
        /// Converts the colorspace of an image (in-place)
        /// </summary>
        /// <param name="cs">Colorspace to convert into</param>
        /// <returns>Self</returns>
        public ComponentsBuffer ChangeColorSpace(ColorSpace cs)
        {
            // Colorspace is already correct
            if (ColorModel.Colorspace == cs) return this;

            byte[] ycbcr = new byte[3];
            byte[] rgb = new byte[3];

            if (ColorModel.Colorspace == ColorSpace.RGB && cs == ColorSpace.YCbCr)
            {
                /*
                 *  Y' =       + 0.299    * R'd + 0.587    * G'd + 0.114    * B'd
                    Cb = 128   - 0.168736 * R'd - 0.331264 * G'd + 0.5      * B'd
                    Cr = 128   + 0.5      * R'd - 0.418688 * G'd - 0.081312 * B'd
                 * 
                 */

                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {
                        YCbCr.fromRGB(ref Raster[0][x, y], ref Raster[1][x, y], ref Raster[2][x, y]);
                    }

                ColorModel.Colorspace = ColorSpace.YCbCr;
            }
            else if (ColorModel.Colorspace == ColorSpace.YCbCr && cs == ColorSpace.RGB)
            {

                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {
                        // 0 is LUMA
                        // 1 is BLUE
                        // 2 is RED

                        YCbCr.toRGB(ref Raster[0][x, y], ref Raster[1][x, y], ref Raster[2][x, y]);
                    }

                ColorModel.Colorspace = ColorSpace.RGB;
            }
            else if (ColorModel.Colorspace == ColorSpace.Gray && cs == ColorSpace.YCbCr)
            {
                // To convert to YCbCr, we just add two 128-filled chroma channels

                byte[,] Cb = new byte[Width, Height];
                byte[,] Cr = new byte[Width, Height];

                for (int x = 0; x < Width; x++)
                    for (int y = 0; y < Height; y++)
                    {
                        Cb[x, y] = 128; Cr[x, y] = 128;
                    }

                Raster = new byte[][,] { Raster[0], Cb, Cr };

                ColorModel.Colorspace = ColorSpace.YCbCr;
            }
            else if (ColorModel.Colorspace == ColorSpace.Gray && cs == ColorSpace.RGB)
            {
                ChangeColorSpace(ColorSpace.YCbCr);
                ChangeColorSpace(ColorSpace.RGB);
            }
            else
            {
                throw new Exception("Colorspace conversion not supported.");
            }

            return this;
        }

        public unsafe void CopyPixels(IntPtr dataPointer, int sizeInBytes)
        {
            var colorBuffer = new byte[Width * Height * ComponentCount];
            int counter = 0;
            for (int i = 0; i< Height; ++i)
            {
                for (int j = 0; j < Width; ++j)
                {
                    for (int k = 0; k < Raster.Length; ++k)
                    {
                        colorBuffer[counter] = Raster[k][j, i];
                        counter++;
                    }
                }
            }

            fixed (byte* el = &colorBuffer[0])
            {
                IntPtr srcPtr = (IntPtr)el;
                Utilities.CopyMemory(dataPointer, srcPtr, sizeInBytes);
            }

            Array.Clear(colorBuffer, 0, colorBuffer.Length);
        }
        
        public byte[] GetPixelBuffer()
        {
            var colorBuffer = new byte[Width * Height * ComponentCount];
            int counter = 0;
            for (int i = 0; i< Height; ++i)
            {
                for (int j = 0; j < Width; ++j)
                {
                    for (int k = 0; k < Raster.Length; ++k)
                    {
                        colorBuffer[counter] = Raster[k][j, i];
                        if (k == 3)
                        {
                            colorBuffer[counter] = 255;
                        }
                        counter++;
                    }
                }
            }

            return colorBuffer;
        }

        public static byte[][,] CreateRaster(int width, int height, int bands)
        {
            // Create the raster
            byte[][,] raster = new byte[bands][,];
            for (int b = 0; b < bands; b++)
                raster[b] = new byte[width, height];
            return raster;
        }

        delegate void ConvertColor(ref byte c1, ref byte c2, ref byte c3);
    }
}
