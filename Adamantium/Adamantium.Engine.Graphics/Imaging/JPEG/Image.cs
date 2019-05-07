/// Copyright (c) 2008 Jeffrey Powers for Fluxcapacity Open Source.
/// Under the MIT License, details: License.txt.

using System;

namespace Adamantium.Engine.Graphics.Imaging.JPEG
{
    //public class Image
    //{
    //    public byte[][,] Raster { get; private set; }
    //    public ColorModel ColorModel { get; }

    //    /// <summary> X density (dots per inch).</summary>
    //    public double DensityX { get; set; }
    //    /// <summary> Y density (dots per inch).</summary>
    //    public double DensityY { get; set; }

    //    public int ComponentCount => Raster.Length;

    //    /// <summary>
    //    /// Converts the colorspace of an image (in-place)
    //    /// </summary>
    //    /// <param name="cs">Colorspace to convert into</param>
    //    /// <returns>Self</returns>
    //    public Image ChangeColorSpace(ColorSpace cs)
    //    {
    //        // Colorspace is already correct
    //        if (ColorModel.Colorspace == cs) return this;

    //        byte[] ycbcr = new byte[3];
    //        byte[] rgb = new byte[3];

    //        if (ColorModel.Colorspace == ColorSpace.RGB && cs == ColorSpace.YCbCr)
    //        {
    //            /*
    //             *  Y' =       + 0.299    * R'd + 0.587    * G'd + 0.114    * B'd
    //                Cb = 128   - 0.168736 * R'd - 0.331264 * G'd + 0.5      * B'd
    //                Cr = 128   + 0.5      * R'd - 0.418688 * G'd - 0.081312 * B'd
    //             * 
    //             */

    //            for (int x = 0; x < Width; x++)
    //                for (int y = 0; y < Height; y++)
    //                {
    //                    YCbCr.fromRGB(ref Raster[0][x, y], ref Raster[1][x, y], ref Raster[2][x, y]);
    //                }

    //            ColorModel.Colorspace = ColorSpace.YCbCr;


    //        }
    //        else if (ColorModel.Colorspace == ColorSpace.YCbCr && cs == ColorSpace.RGB)
    //        {

    //            for (int x = 0; x < Width; x++)
    //                for (int y = 0; y < Height; y++)
    //                {
    //                    // 0 is LUMA
    //                    // 1 is BLUE
    //                    // 2 is RED

    //                    YCbCr.toRGB(ref Raster[0][x, y], ref Raster[1][x, y], ref Raster[2][x, y]);
    //                }

    //            ColorModel.Colorspace = ColorSpace.RGB;
    //        }
    //        else if (ColorModel.Colorspace == ColorSpace.Gray && cs == ColorSpace.YCbCr)
    //        {
    //            // To convert to YCbCr, we just add two 128-filled chroma channels

    //            byte[,] Cb = new byte[Width, Height];
    //            byte[,] Cr = new byte[Width, Height];

    //            for (int x = 0; x < Width; x++)
    //                for (int y = 0; y < Height; y++)
    //                {
    //                    Cb[x, y] = 128; Cr[x, y] = 128;
    //                }

    //            Raster = new byte[][,] { Raster[0], Cb, Cr };

    //            ColorModel.Colorspace = ColorSpace.YCbCr;
    //        }
    //        else if (ColorModel.Colorspace == ColorSpace.Gray && cs == ColorSpace.RGB)
    //        {
    //            ChangeColorSpace(ColorSpace.YCbCr);
    //            ChangeColorSpace(ColorSpace.RGB);
    //        }
    //        else
    //        {
    //            throw new Exception("Colorspace conversion not supported.");
    //        }

    //        return this;
    //    }

    //    public int Width { get; private set; }
    //    public int Height { get; private set; }

    //    public Image(ColorModel cm, byte[][,] raster)
    //    {
    //        Width = raster[0].GetLength(0);
    //        Height = raster[0].GetLength(1);

    //        ColorModel = cm;
    //        Raster = raster;
    //    }

    //    public static byte[][,] CreateRaster(int Width, int Height, int bands)
    //    {
    //        // Create the raster
    //        byte[][,] raster = new byte[bands][,];
    //        for (int b = 0; b < bands; b++)
    //            raster[b] = new byte[Width, Height];
    //        return raster;
    //    }

    //    delegate void ConvertColor(ref byte c1, ref byte c2, ref byte c3);

    //}
}
