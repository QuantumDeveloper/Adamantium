using System;
using System.Collections.Generic;

namespace Adamantium.Imaging.Png
{
    internal static class PNGFilter
    {
        /*
        Paeth predictor, used by PNG filter type 4
        The parameters are of type short, but should come from unsigned chars, the shorts
        are only needed to make the paeth calculation correct.
        */
        internal static byte PaethPredictor(short a, short b, short c)
        {
            short pa = (short)Math.Abs(b - c);
            short pb = (short)Math.Abs(a - c);
            short pc = (short)Math.Abs(a + b - c - c);

            if (pc < pa && pc < pb) return (byte)c;
            else if (pb < pa) return (byte)b;
            else return (byte)a;
        }

        internal static unsafe uint Unfilter(byte* rawBuffer, byte* inputData, int width, int height, int bpp)
        {
            /*
            For PNG filter method 0
            this function unfilters a single image (e.g. without interlacing this is called once, with Adam7 seven times)
            rawBuffer must have enough bytes allocated already, in must have the scanlines + 1 filtertype byte per scanline
            w and h are image dimensions or dimensions of reduced image, bpp is bits per pixel
            in and out are allowed to be the same memory address (but aren't the same size since in has the extra filter bytes)
            */

            byte* prevLine = null;
            uint error = 0;

            /*bytewidth is used for filtering, is 1 when bpp < 8, number of bytes per pixel otherwise*/
            int byteWidth = (bpp + 7) / 8;
            int lineBytes = (width * bpp + 7) / 8;

            for (int i = 0; i < height; ++i)
            {
                var outIndex = lineBytes * i;
                var inindex = (1 + lineBytes) * i; /*the extra filterbyte added to each row*/
                byte filterType = inputData[inindex];

                error = UnfilterScanline(&rawBuffer[outIndex], &inputData[inindex + 1], prevLine, byteWidth, filterType, lineBytes);

                prevLine = &rawBuffer[outIndex];
            }

            return error;
        }

        private static unsafe uint UnfilterScanline(byte* recon, byte* scanline, byte* precon, int bytewidth, byte filterType, int length)
        {
            int i = 0;
            switch (filterType)
            {
                case 0:
                    for (i = 0; i != length; ++i)
                    {
                        recon[i] = scanline[i];
                    }
                    break;
                case 1:
                    for (i = 0; i != bytewidth; ++i) recon[i] = scanline[i];
                    for (i = bytewidth; i < length; ++i) recon[i] = (byte)(scanline[i] + recon[i - bytewidth]);
                    break;
                case 2:
                    if (precon != null)
                    {
                        for (i = 0; i != length; ++i) recon[i] = (byte)(scanline[i] + precon[i]);
                    }
                    else
                    {
                        for (i = 0; i != length; ++i) recon[i] = scanline[i];
                    }
                    break;
                case 3:
                    if (precon != null)
                    {
                        for (i = 0; i != bytewidth; ++i) recon[i] = (byte)(scanline[i] + (precon[i] >> 1));
                        for (i = bytewidth; i < length; ++i) recon[i] = (byte)(scanline[i] + ((recon[i - bytewidth] + precon[i]) >> 1));
                    }
                    else
                    {
                        for (i = 0; i != bytewidth; ++i) recon[i] = scanline[i];
                        for (i = bytewidth; i < length; ++i) recon[i] = (byte)(scanline[i] + (recon[i - bytewidth] >> 1));
                    }
                    break;
                case 4:
                    if (precon != null)
                    {
                        for (i = 0; i != bytewidth; ++i)
                        {
                            recon[i] = (byte)(scanline[i] + precon[i]); /*paethPredictor(0, precon[i], 0) is always precon[i]*/
                        }
                        for (i = bytewidth; i < length; ++i)
                        {
                            recon[i] = (byte)(scanline[i] + PaethPredictor(recon[i - bytewidth], precon[i], precon[i - bytewidth]));
                        }
                    }
                    else
                    {
                        for (i = 0; i != bytewidth; ++i)
                        {
                            recon[i] = scanline[i];
                        }
                        for (i = bytewidth; i < length; ++i)
                        {
                            /*paethPredictor(recon[i - bytewidth], 0, 0) is always recon[i - bytewidth]*/
                            recon[i] = (byte)(scanline[i] + recon[i - bytewidth]);
                        }
                    }
                    break;
                default: return 36; /*error: unexisting filter type given*/
            }
            return 0;
        }

        internal static unsafe uint Filter(byte* outData, byte* inData, uint width, uint height,
            PNGColorMode info, PNGEncoderSettings settings)
        {
            /*
            For PNG filter method 0
            out must be a buffer with as size: h + (w * h * bpp + 7) / 8, because there are
            the scanlines with 1 extra byte per scanline
            */
            int x, y;
            uint error = 0;
            var bpp = PNGColorConvertion.GetBitsPerPixel(info);
            /*the width of a scanline in bytes, not including the filter type*/
            var lineBytes = (width * bpp + 7) / 8;
            /*bytewidth is used for filtering, is 1 when bpp < 8, number of bytes per pixel otherwise*/
            var byteWidth = (bpp + 7) / 8;

            FilterStrategy strategy = settings.FilterStrategy;

            byte* prevLine = null;

            /*
            There is a heuristic called the minimum sum of absolute differences heuristic, suggested by the PNG standard:
            *  If the image type is Palette, or the bit depth is smaller than 8, then do not filter the image (i.e.
                use fixed filtering, with the filter None).
            *  (The other case) If the image type is Grayscale or RGB (with or without Alpha), and the bit depth is
                not smaller than 8, then use adaptive filtering heuristic as follows: independently for each row, apply
                all five filters and select the filter that produces the smallest sum of absolute values per row.
            This heuristic is used if filter strategy is LFS_MINSUM and filter_palette_zero is true.
            If filter_palette_zero is true and filter_strategy is not LFS_MINSUM, the above heuristic is followed,
            but for "the other case", whatever strategy filter_strategy is set to instead of the minimum sum
            heuristic is used.
            */
            if (settings.FilterPaletteZero 
                && (info.ColorType == PNGColorType.Palette || info.BitDepth < 8))
            {
                strategy = FilterStrategy.Zero;
            }

            if (bpp == 0) return 31; /*error: invalid color type*/

            if (strategy == FilterStrategy.Zero)
            {
                for (y = 0; y != height; ++y)
                {
                    var outIndex = (1 + lineBytes) * y; /*the extra filterbyte added to each row*/
                    var inIndex = lineBytes * y;
                    outData[outIndex] = 0; /*filter type byte*/
                    FilterScalnline(&outData[outIndex + 1], &inData[inIndex], prevLine, (int)lineBytes, (int)byteWidth, 0);
                    prevLine = &inData[inIndex];
                }
            }
            else if (strategy == FilterStrategy.MinSum)
            {
                /*adaptive filtering*/
                int[] sum = new int[5];
                List<byte[]> attempt = new List<byte[]>(); /*five filtering attempts, one for each filter type*/
                int smallest = 0;
                byte type, bestType = 0;

                for (type = 0; type != 5; ++type)
                {
                    attempt.Add(new byte[lineBytes]);
                }

                for (y = 0; y != height; ++y)
                {
                    /*try the 5 filter types*/
                    for (type = 0; type != 5; ++type)
                    {
                        fixed (byte* attemptPtr = &attempt[type][0])
                        {
                            FilterScalnline(attemptPtr, &inData[y * lineBytes], prevLine, (int)lineBytes, (int)byteWidth, type);
                        }
                        /*calculate the sum of the result*/
                        sum[type] = 0;
                        if (type == 0)
                        {
                            for (x = 0; x != lineBytes; ++x)
                            {
                                sum[type] += (byte)attempt[type][x];
                            }
                        }
                        else
                        {
                            for (x = 0; x != lineBytes; ++x)
                            {
                                /*For differences, each byte should be treated as signed, values above 127 are negative
                                (converted to signed char). Filtertype 0 isn't a difference though, so use unsigned there.
                                This means filtertype 0 is almost never chosen, but that is justified.*/
                                byte s = attempt[type][x];
                                sum[type] += s < 128 ? s : (255 - s);
                            }
                        }

                        /*check if this is smallest sum (or if type == 0 it's the first case so always store the values)*/
                        if (type == 0 || sum[type] < smallest)
                        {
                            bestType = type;
                            smallest = sum[type];
                        }
                    }

                    prevLine = &inData[y * lineBytes];

                    /*now fill the out values*/
                    outData[y * (lineBytes + 1)] = bestType; /*the first byte of a scanline will be the filter type*/
                    for (x = 0; x != lineBytes; ++x)
                    {
                        outData[y * (lineBytes + 1) + 1 + x] = attempt[bestType][x];
                    }
                }

                attempt.Clear();
            }
            else if (strategy == FilterStrategy.Entropy)
            {
                float[] sum = new float[5];
                List<byte[]> attempt = new List<byte[]>(); /*five filtering attempts, one for each filter type*/
                float smallest = 0;
                int type;
                int bestType = 0;
                uint[] count = new uint[256];

                for (type = 0; type != 5; ++type)
                {
                    attempt.Add(new byte[lineBytes]);
                }

                for (y = 0; y != height; ++y)
                {
                    /*try the 5 filter types*/
                    for (type = 0; type != 5; ++type)
                    {
                        fixed (byte* attemptPtr = &attempt[type][0])
                        {
                            FilterScalnline(attemptPtr, &inData[y * lineBytes], prevLine, (int)lineBytes, (int)byteWidth, (byte)type);
                        }
                        for (x = 0; x != 256; ++x)
                        {
                            count[x] = 0;
                        }
                        for (x = 0; x != lineBytes; ++x)
                        {
                            ++count[attempt[type][x]];
                        }
                        ++count[type]; /*the filter type itself is part of the scanline*/
                        sum[type] = 0;
                        for (x = 0; x != 256; ++x)
                        {
                            float p = count[x] / (float)(lineBytes + 1);
                            sum[type] += count[x] == 0 ? 0 : (float)Math.Log2(1 / p) * p;
                        }
                        /*check if this is smallest sum (or if type == 0 it's the first case so always store the values)*/
                        if (type == 0 || sum[type] < smallest)
                        {
                            bestType = type;
                            smallest = sum[type];
                        }
                    }

                    prevLine = &inData[y * lineBytes];

                    /*now fill the out values*/
                    outData[y * (lineBytes + 1)] = (byte)bestType; /*the first byte of a scanline will be the filter type*/
                    for (x = 0; x != lineBytes; ++x)
                    {
                        outData[y * (lineBytes + 1) + 1 + x] = attempt[bestType][x];
                    }
                }

                attempt.Clear();
            }
            else if (strategy == FilterStrategy.BruteForce)
            {
                /*brute force filter chooser.
                deflate the scanline after every filter attempt to see which one deflates best.
                This is very slow and gives only slightly smaller, sometimes even larger, result*/
                int[] size = new int[5];
                List<byte[]> attempt = new List<byte[]>(); /*five filtering attempts, one for each filter type*/
                int smallest = 0;
                int type = 0;
                int bestType = 0;
                List<byte> dummy = new List<byte>();

                /*use fixed tree on the attempts so that the tree is not adapted to the filtertype on purpose,
                to simulate the true case where the tree is the same for the whole image. Sometimes it gives
                better result with dynamic tree anyway. Using the fixed tree sometimes gives worse, but in rare
                cases better compression. It does make this a bit less slow, so it's worth doing this.*/

                settings.BType = 1;

                for (type = 0; type != 5; ++type)
                {
                    attempt.Add(new byte[lineBytes]);
                }

                PNGCompressor compressor = new PNGCompressor();
                for (y = 0; y != height; ++y) /*try the 5 filter types*/
                {
                    for (type = 0; type != 5; ++type)
                    {
                        fixed (byte* attemptPtr = &attempt[type][0])
                        {
                            FilterScalnline(attemptPtr, &inData[y * lineBytes], prevLine, (int)lineBytes, (int)byteWidth, (byte)type);
                        }

                        size[type] = 0;
                        error = compressor.Compress(attempt[type], settings, dummy);
                        /*check if this is smallest size (or if type == 0 it's the first case so always store the values)*/
                        if (type == 0 || size[type] < smallest)
                        {
                            bestType = type;
                            smallest = size[type];
                        }
                    }

                    prevLine = &inData[y * lineBytes];
                    outData[y * (lineBytes + 1)] = (byte)bestType; /*the first byte of a scanline will be the filter type*/
                    for (x = 0; x != lineBytes; ++x)
                    {
                        outData[y * (lineBytes + 1) + 1 + x] = attempt[bestType][x];
                    }
                }

                attempt.Clear();
            }
            else
            {
                error = 88;
            }

            return error;
        }

        private static unsafe void FilterScalnline(byte* outData, byte* scanline, byte* prevline, int length, int byteWidth, byte filterType)
        {
            int i = 0;
            switch (filterType)
            {
                case 0: /*None*/
                    for (i = 0; i != length; ++i)
                    {
                        outData[i] = scanline[i];
                    }
                    break;
                case 1: /*Sub*/
                    for (i = 0; i != byteWidth; ++i)
                    {
                        outData[i] = scanline[i];
                    }
                    for (i = byteWidth; i < length; ++i)
                    {
                        outData[i] = (byte)(scanline[i] - scanline[i - byteWidth]);
                    }
                    break;
                case 2: /*Up*/
                    if (prevline != null)
                    {
                        for (i = 0; i != length; ++i)
                        {
                            outData[i] = (byte)(scanline[i] - prevline[i]);
                        }
                    }
                    else
                    {
                        for (i = 0; i != length; ++i)
                        {
                            outData[i] = scanline[i];
                        }
                    }
                    break;
                case 3: /*Average*/
                    if (prevline != null)
                    {
                        for (i = 0; i < byteWidth; ++i)
                        {
                            outData[i] = (byte)(scanline[i] - (prevline[i] >> 1));
                        }
                        for (i = byteWidth; i < length; ++i)
                        {
                            outData[i] = (byte)(scanline[i] - (byte)((scanline[i - byteWidth] + prevline[i] >> 1)));
                        }
                    }
                    else
                    {
                        for (i = 0; i != byteWidth; ++i)
                        {
                            outData[i] = scanline[i];
                        }
                        for (i = byteWidth; i < length; ++i)
                        {
                            outData[i] = (byte)(scanline[i] - (byte)(scanline[i - byteWidth] >> 1));
                        }
                    }
                    break;
                case 4: /*Paeth*/
                    if (prevline != null)
                    {
                        /*paethPredictor(0, prevline[i], 0) is always prevline[i]*/
                        for (i = 0; i != byteWidth; ++i)
                        {
                            outData[i] = (byte)(scanline[i] - prevline[i]);
                        }
                        for (i = byteWidth; i < length; ++i)
                        {
                            outData[i] = (byte)(scanline[i] - PNGFilter.PaethPredictor(scanline[i - byteWidth], prevline[i], prevline[i - byteWidth]));
                        }
                    }
                    else
                    {
                        for (i = 0; i != byteWidth; ++i)
                        {
                            outData[i] = scanline[i];
                        }
                        /*paethPredictor(scanline[i - bytewidth], 0, 0) is always scanline[i - bytewidth]*/
                        for (i = byteWidth; i < length; ++i)
                        {
                            outData[i] = (byte)(scanline[i] - scanline[i - byteWidth]);
                        }
                    }
                    break;
                default: return; /*unexisting filter type given*/
            }
        }
    }
}
