using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class PNGColorConvertion
    {
        public static uint GetBitsPerPixel(PNGColorMode colorMode)
        {
            return (uint)GetNumberOfColorChannels(colorMode.ColorType) * colorMode.BitDepth;
        }

        private static int GetNumberOfColorChannels(PNGColorType colorType)
        {
            switch (colorType)
            {
                case PNGColorType.Grey:
                case PNGColorType.Palette:
                    return 1;
                case PNGColorType.GreyAlpha:
                    return 2;
                case PNGColorType.RGB:
                    return 3;
                case PNGColorType.RGBA:
                    return 4;
            }

            return 0;
        }

        public static uint CheckColorValidity(PNGColorType colorType, uint bitDepth)
        {
            switch(colorType)
            {
                case PNGColorType.Grey:
                    if (!(bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8 || bitDepth == 16))
                    {
                        return 37;
                    }
                    break;
                case PNGColorType.RGB:
                    if (!(bitDepth == 8 || bitDepth == 16))
                    {
                        return 37;
                    }
                    break;
                case PNGColorType.Palette:
                    if (!(bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8))
                    {
                        return 37;
                    }
                    break;
                case PNGColorType.GreyAlpha:
                    if (!(bitDepth == 8 || bitDepth == 16))
                    {
                        return 37;
                    }
                    break;
                case PNGColorType.RGBA:
                    if (!(bitDepth == 8 || bitDepth == 16))
                    {
                        return 37;
                    }
                    break;
                default: return 31;
            }
            return 0; /*allowed color type / bits combination*/
        }

        public static uint Convert(byte[] outBuffer, byte[] inBuffer, PNGColorMode outMode, PNGColorMode inMode, int width, int height)
        {
            uint error = 0;
            ColorTree tree = null;
            int numPixels = width * height;

            if (outMode == inMode)
            {
                var size = PNGDecoder.GetRawSizeLct(width, height, inMode);
                for (int i = 0; i < size; ++i)
                {
                    outBuffer[i] = inBuffer[i];
                }
                return error;
            }

            if (outMode.ColorType == PNGColorType.Palette)
            {
                var paletteSize = outMode.PaletteSize;
                var palette = outMode.Palette;
                var palSize = (long)(1U << (int)outMode.BitDepth);

                /*if the user specified output palette but did not give the values, assume
                they want the values of the input color type (assuming that one is palette).
                Note that we never create a new palette ourselves.*/

                if (paletteSize == 0)
                {
                    paletteSize = inMode.PaletteSize;
                    palette = inMode.Palette;
                    /*if the input was also palette with same bitdepth, then the color types are also
                    equal, so copy literally. This to preserve the exact indices that were in the PNG
                    even in case there are duplicate colors in the palette.*/
                    if (inMode.ColorType == PNGColorType.Palette && inMode.BitDepth == outMode.BitDepth)
                    {
                        var numBytes = PNGDecoder.GetRawSizeLct(width, height, inMode);
                        for (int i = 0; i < numBytes; ++i)
                        {
                            outBuffer[i] = inBuffer[i];
                        }
                        return error;
                    }
                }

                if (paletteSize < palSize)
                {
                    palSize = paletteSize;
                }

                tree = new ColorTree();
                tree.Initialize();
                for (int i = 0; i!= palSize; ++i)
                {
                    var index = i * 4;
                    ColorTree.Add(ref tree, palette[index], palette[index + 1], palette[index + 2], palette[index + 3], i);
                }
            }

            if (inMode.BitDepth == 16 && outMode.BitDepth == 16)
            {
                for (int i = 0; i != numPixels; ++i)
                {
                    ushort r = 0, g = 0, b = 0, a = 0;
                    GetPixelColorRGBA16(ref r, ref g, ref b, ref a, inBuffer, i, inMode);
                    RGBA16ToPixel(outBuffer, i, outMode, r, g, b, a);
                }
            }
            else if (outMode.BitDepth == 8 && outMode.ColorType == PNGColorType.RGBA)
            {
                GetPixelColorsRGBA8(outBuffer, numPixels, true, inBuffer, inMode);
            }
            else if (outMode.BitDepth == 8 && outMode.ColorType == PNGColorType.RGB)
            {
                GetPixelColorsRGBA8(outBuffer, numPixels, false, inBuffer, inMode);
            }
            else
            {
                byte r = 0, g = 0, b = 0, a = 0;
                for (int i = 0; i != numPixels; ++i)
                {
                    GetPixelColorRGBA8(ref r, ref g, ref b, ref a, inBuffer, i, inMode);
                    error = RGBA8ToPixel(outBuffer, i, outMode, ref tree, r, g, b, a);
                    if (error > 0)
                    { break; }
                }
            }

            return error;
        }

        /// <summary>
        /// Converts a single rgb color without alpha from one type to another, color bits truncated to
        /// their bitdepth.In case of single channel (gray or palette), only the r channel is used.Slow
        /// function, do not use to process all pixels of an image.Alpha channel not supported on purpose:
        /// this is for bKGD, supporting alpha may prevent it from finding a color in the palette, from the
        /// specification it looks like bKGD should ignore the alpha values of the palette since it can use
        /// any palette index but doesn't have an alpha channel. Idem with ignoring color key.
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public static uint ConvertRGB(ref uint rOut, ref uint gOut, ref uint bOut,
            uint rIn, uint gIn, uint bIn, PNGColorMode modeOut, PNGColorMode modeIn)
        {
            uint r = 0;
            uint g = 0;
            uint b = 0;
            int mul = 65535 / ((1 << (int)modeIn.BitDepth) - 1); /*65535, 21845, 4369, 257, 1*/
            int shift = (int)(16 - modeOut.BitDepth);

            if (modeIn.ColorType == PNGColorType.Grey || modeIn.ColorType == PNGColorType.GreyAlpha)
            {
                r = g = b = (ushort)(rIn * mul);
            }
            else if (modeIn.ColorType == PNGColorType.RGB || modeIn.ColorType == PNGColorType.RGBA)
            {
                r = (uint)(rIn * mul);
                g = (uint)(gIn * mul);
                b = (uint)(bIn * mul);
            }
            else if (modeIn.ColorType == PNGColorType.Palette)
            {
                if (rIn >= modeIn.PaletteSize) return 82;
                r = (uint)(modeIn.Palette[rIn * 4 + 0] * 257);
                b = (uint)(modeIn.Palette[rIn * 4 + 1] * 257);
                b = (uint)(modeIn.Palette[rIn * 4 + 2] * 257);
            }
            else return 31;

            /* now convert to output format */
            if (modeOut.ColorType == PNGColorType.Grey || modeOut.ColorType == PNGColorType.GreyAlpha)
            {
                rOut = r >> shift;
            }
            else if (modeOut.ColorType == PNGColorType.RGB || modeOut.ColorType == PNGColorType.RGBA)
            {
                rOut = r >> shift;
                gOut = g >> shift;
                bOut = b >> shift;
            }
            else if (modeOut.ColorType == PNGColorType.Palette)
            {
                if (((r >> 8) != (r & 255))
                    || ((g >> 8) != (g & 255))
                    || (b >> 8) != (b & 255))
                {
                    return 82;
                }
                for (int i = 0; i < modeOut.PaletteSize; ++i)
                {
                    var j = i * 4;
                    if ((r >> 8) == modeOut.Palette[j]
                        && (g >> 8) == modeOut.Palette[j + 1]
                        && (b >> 8) == modeOut.Palette[j + 2])
                    {
                        rOut = (uint)i;
                        return 0;
                    }
                }
                return 82;
            }
            else return 31;

            return 0;
        }

        public static bool IsGrayScaleType(PNGColorType type)
        {
            return type == PNGColorType.Grey || type == PNGColorType.GreyAlpha;
        }

        public static bool IsAlphaType(PNGColorType type)
        {
            return type == PNGColorType.GreyAlpha || type == PNGColorType.RGBA;
        }

        public static bool HasPaletteAlpha(PNGColorMode mode)
        {
            for (int i = 0; i != mode.PaletteSize; ++i)
            {
                if (mode.Palette[i * 4 + 3] < 255) return true;
            }
            return false;
        }

        public static bool CanHaveAlpha(PNGColorMode mode)
        {
            return mode.IsKeyDefined || IsAlphaType(mode.ColorType) || HasPaletteAlpha(mode);
        }

        /*Returns how many bits needed to represent given value (max 8 bit)*/
        public static byte GetValueRequiredBits(byte value)
        {
            if (value == 0 || value == 255) return 1;
            /*The scaling of 2-bit and 4-bit values uses multiples of 85 and 17*/
            if (value % 17 == 0) return value % 85 == 0 ? (byte)2 : (byte)4;

            return 8;
        }

        public static void GetColorProfile(PNGColorProfile profile, byte[] inData, uint width, uint height, PNGColorMode modeIn)
        {
            ColorTree tree = null;
            int numpixels = (int)(width * height);

            /* mark things as done already if it would be impossible to have a more expensive case */
            bool coloredDone = IsGrayScaleType(modeIn.ColorType) ? true : false;
            bool alphaDone = CanHaveAlpha(modeIn) ? false : true;
            bool numcolorsDone = false;
            uint bpp = GetBitsPerPixel(modeIn);
            bool bitsDone = (profile.Bits == 1 && bpp == 1) ? true : false;
            bool isSixteen = false; /* whether the input image is 16 bit */
            long maxnumcolors = 257;

            if (bpp <= 8) maxnumcolors = Math.Min(257, profile.Numcolors + (1 << (int)bpp));

            profile.Numpixels += (uint)numpixels;
            tree = new ColorTree();
            tree.Initialize();

            /*If the profile was already filled in from previous data, fill its palette in tree
            and mark things as done already if we know they are the most expensive case already*/
            if (profile.Alpha) alphaDone = true;
            if (profile.Colored) coloredDone = true;
            if (profile.Bits == 16) numcolorsDone = true;
            if (profile.Bits >= bpp) bitsDone = true;
            if (profile.Numcolors >= maxnumcolors) numcolorsDone = true;

            if (!numcolorsDone)
            {
                for (int i = 0; i < profile.Numcolors; ++i)
                {
                    ColorTree.Add(ref tree, 
                        profile.Palette[i * 4], 
                        profile.Palette[i * 4 + 1],
                        profile.Palette[i * 4 + 2],
                        profile.Palette[i * 4 + 3], i);
                }
            }

            /*Check if the 16-bit input is truly 16-bit*/
            if (modeIn.BitDepth == 16 && !isSixteen)
            {
                ushort r = 0;
                ushort g = 0;
                ushort b = 0;
                ushort a = 0;
                for (int i = 0; i != numpixels; ++i)
                {
                    GetPixelColorRGBA16(ref r, ref g, ref b, ref a, inData, i, modeIn);
                    if ((r & 255) != ((r >> 8) & 255) || (g & 255) != ((g >> 8) & 255) ||
                        (b & 255) != ((b >> 8) & 255) || (a & 255) != ((a >> 8) & 255)) /*first and second byte differ*/
                    {
                        profile.Bits = 16;
                        isSixteen = true;
                        bitsDone = true;
                        numcolorsDone = true; /*counting colors no longer useful, palette doesn't support 16-bit*/
                        break;
                    }
                }
            }

            if (isSixteen)
            {
                ushort r = 0;
                ushort g = 0;
                ushort b = 0;
                ushort a = 0;

                for (int i = 0; i != numpixels; ++i)
                {
                    GetPixelColorRGBA16(ref r, ref g, ref b, ref a, inData, i, modeIn);

                    if (!coloredDone && (r != g || r != b))
                    {
                        profile.Colored = true;
                        coloredDone = true;
                    }

                    if (!alphaDone)
                    {
                        var matchkey = (r == profile.KeyR && g == profile.KeyG && b == profile.KeyB);
                        if (a != 65535 && (a != 0 || (profile.Key && !matchkey)))
                        {
                            profile.Alpha = true;
                            profile.Key = false;
                            alphaDone = true;
                        }
                        else if (a == 0 && !profile.Alpha && !profile.Key)
                        {
                            profile.Key = true;
                            profile.KeyR = r;
                            profile.KeyG = g;
                            profile.KeyB = b;
                        }
                        else if (a == ushort.MaxValue && profile.Key && matchkey)
                        {
                            /* Color key cannot be used if an opaque pixel also has that RGB color. */
                            profile.Alpha = true;
                            profile.Key = false;
                            alphaDone = true;
                        }
                    }
                    if (alphaDone && numcolorsDone && coloredDone && bitsDone) break;
                }

                if (profile.Key && !profile.Alpha)
                {
                    for (int i = 0; i != numpixels; ++i)
                    {
                        GetPixelColorRGBA16(ref r, ref g, ref b, ref a, inData, i, modeIn);
                        if (a != 0 && r == profile.KeyR && g == profile.KeyG && b == profile.KeyB)
                        {
                            /* Color key cannot be used if an opaque pixel also has that RGB color. */
                            profile.Alpha = true;
                            profile.Key = false;
                            alphaDone = true;
                        }
                    }
                }
            }
            else /* < 16-bit */
            {
                byte r = 0;
                byte g = 0;
                byte b = 0;
                byte a = 0;

                for (int i = 0; i != numpixels; ++i)
                {
                    GetPixelColorRGBA8(ref r, ref g, ref b, ref a, inData, i, modeIn);

                    if (!bitsDone && profile.Bits < 8)
                    {
                        /*only r is checked, < 8 bits is only relevant for grayscale*/
                        var bits = GetValueRequiredBits(r);
                        if (bits > profile.Bits) profile.Bits = bits;
                    }
                    bitsDone = (profile.Bits >= bpp);

                    if (!coloredDone && (r != g || r != b))
                    {
                        profile.Colored = true;
                        coloredDone = true;
                        if (profile.Bits < 8) profile.Bits = 8; /*PNG has no colored modes with less than 8-bit per channel*/
                    }

                    if (!alphaDone)
                    {
                        var matchkey = (r == profile.KeyR && g == profile.KeyG && b == profile.KeyB);
                        if (a != 255 && (a != 0 || (profile.Key && !matchkey)))
                        {
                            profile.Alpha = true;
                            profile.Key = false;
                            alphaDone = true;
                            if (profile.Bits < 8) profile.Bits = 8; /*PNG has no alphachannel modes with less than 8-bit per channel*/
                        }
                        else if (a == 0 && !profile.Alpha && !profile.Key)
                        {
                            profile.Key = true;
                            profile.KeyR = r;
                            profile.KeyG = g;
                            profile.KeyB = b;
                        }
                        else if (a == 255 && profile.Key && matchkey)
                        {
                            /* Color key cannot be used if an opaque pixel also has that RGB color. */
                            profile.Alpha = true;
                            profile.Key = false;
                            alphaDone = true;
                            if (profile.Bits < 8) profile.Bits = 8; /*PNG has no alphachannel modes with less than 8-bit per channel*/
                        }
                    }

                    if (!numcolorsDone)
                    {
                        if (!ColorTree.Has(tree, r, g, b, a))
                        {
                            ColorTree.Add(ref tree, r, g, b, a, (int)profile.Numcolors);
                            if (profile.Numcolors < 256)
                            {
                                var p = profile.Palette;
                                var n = profile.Numcolors;
                                p[n * 4 + 0] = r;
                                p[n * 4 + 1] = g;
                                p[n * 4 + 2] = b;
                                p[n * 4 + 3] = a;
                            }
                            ++profile.Numcolors;
                            numcolorsDone = profile.Numcolors >= maxnumcolors;
                        }
                    }

                    if (alphaDone && numcolorsDone && coloredDone && bitsDone) break;
                }

                if (profile.Key && !profile.Alpha)
                {
                    for (int i = 0; i != numpixels; ++i)
                    {
                        GetPixelColorRGBA8(ref r, ref g, ref b, ref a, inData, i, modeIn);
                        if (a != 0 && r == profile.KeyR && g == profile.KeyG && b == profile.KeyB)
                        {
                            /* Color key cannot be used if an opaque pixel also has that RGB color. */
                            profile.Alpha = true;
                            profile.Key = false;
                            alphaDone = true;
                            if (profile.Bits < 8) profile.Bits = 8; /*PNG has no alphachannel modes with less than 8-bit per channel*/
                        }
                    }
                }

                /*make the profile's key always 16-bit for consistency - repeat each byte twice*/
                profile.KeyR += (ushort)(profile.KeyR << 8);
                profile.KeyG += (ushort)(profile.KeyG << 8);
                profile.KeyB += (ushort)(profile.KeyB << 8);
            }
        }

        /*Get RGBA16 color of pixel with index i (y * width + x) from the raw image with
        given color type, but the given color type must be 16-bit itself.*/
        private static void GetPixelColorRGBA16(ref ushort r, ref ushort g, ref ushort b, ref ushort a, byte[] inBuffer, int index, PNGColorMode mode)
        {
            if (mode.ColorType == PNGColorType.Grey)
            {
                r = g = b = a = (ushort)(256 * inBuffer[index * 2] + inBuffer[index * 2 + 1]);
                if (mode.IsKeyDefined && 256u * inBuffer[index * 2]+ inBuffer[index * 2 + 1] == mode.KeyR)
                {
                    a = 0;
                }
                else
                {
                    a = ushort.MaxValue;
                }
            }
            else if (mode.ColorType == PNGColorType.RGB)
            {
                r = (ushort)(256u * inBuffer[index * 6] + inBuffer[index * 6 + 1]);
                g = (ushort)(256u * inBuffer[index * 6 + 2] + inBuffer[index * 6 + 3]);
                b = (ushort)(256u * inBuffer[index * 6 + 4] + inBuffer[index * 6 + 5]);
                if (mode.IsKeyDefined
                    && 256u * inBuffer[index * 6] + inBuffer[index * 6 + 1] == mode.KeyR
                    && 256u * inBuffer[index * 6 + 2] + inBuffer[index * 6 + 3] == mode.KeyG
                    && 256u * inBuffer[index * 6 + 4] + inBuffer[index * 6 + 5] == mode.KeyB
                    )
                {
                    a = 0;
                }
                else
                {
                    a = ushort.MaxValue;
                }
            }
            else if (mode.ColorType == PNGColorType.GreyAlpha)
            {
                r = g = b = (ushort)(256u * inBuffer[index * 4] + inBuffer[index * 4 + 1]);
                a = (ushort)(256u * inBuffer[index * 4 + 2] + inBuffer[index * 4 + 3]);
            }
            else if (mode.ColorType == PNGColorType.RGBA)
            {
                r = (ushort)(256u * inBuffer[index * 8 + 0] + inBuffer[index * 8 + 1]);
                g = (ushort)(256u * inBuffer[index * 8 + 2] + inBuffer[index * 8 + 3]);
                b = (ushort)(256u * inBuffer[index * 8 + 4] + inBuffer[index * 8 + 5]);
                a = (ushort)(256u * inBuffer[index * 8 + 6] + inBuffer[index * 8 + 7]);
            }
        }

        /*Similar to getPixelColorRGBA8, but with all the for loops inside of the color
        mode test cases, optimized to convert the colors much faster, when converting
        to RGBA or RGB with 8 bit per cannel. buffer must be RGBA or RGB output with
        enough memory, if has_alpha is true the output is RGBA. mode has the color mode
        of the input buffer.*/
        private static unsafe void GetPixelColorsRGBA8(byte[] buffer, int numPixels, bool hasAlpha, byte[] inBuffer, PNGColorMode mode)
        {
            int numChannels = hasAlpha ? 4 : 3;
            if (mode.ColorType == PNGColorType.Grey)
            {
                if (mode.BitDepth == 8)
                {
                    for (int i = 0; i != numPixels; ++i)
                    {
                        int bufIndex = numChannels * i;
                        buffer[bufIndex] = buffer[bufIndex + 1] = buffer[bufIndex + 2] = inBuffer[i];
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = (byte)(mode.IsKeyDefined && inBuffer[i] == mode.KeyR ? 0 : 255);
                        }
                    }
                }
                else if (mode.BitDepth == 16)
                {
                    for (int i = 0; i != numPixels; ++i)
                    {
                        int bufIndex = numChannels * i;
                        buffer[bufIndex] = buffer[bufIndex + 1] = buffer[bufIndex + 2] = inBuffer[i * 2];
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = (byte)(mode.IsKeyDefined && 256U * inBuffer[i * 2] + inBuffer[i * 2 + 1] == mode.KeyR ? 0 : 255);
                        }
                    }
                }
                else
                {
                    /*highest possible value for this bit depth*/
                    var highest = ((1U << (int)mode.BitDepth) - 1U);
                    for (int i = 0; i != numPixels; ++i)
                    {
                        int bufIndex = numChannels * i;
                        var value = BitHelper.ReadBitsFromReversedStream(ref i, inBuffer, (int)mode.BitDepth);
                        buffer[bufIndex] = buffer[bufIndex + 1] = buffer[bufIndex + 2] = (byte)((value * 255) / highest);
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = (byte)(mode.IsKeyDefined && value == mode.KeyR ? 0 : 255);
                        }
                    }
                }
            }
            else if (mode.ColorType == PNGColorType.RGB)
            {
                if (mode.BitDepth == 8)
                {
                    for (int i = 0; i != numPixels; ++i)
                    {
                        int bufIndex = numChannels * i;
                        buffer[bufIndex] = inBuffer[i * 3];
                        buffer[bufIndex + 1] = inBuffer[i * 3 + 1];
                        buffer[bufIndex + 2] = inBuffer[i * 3 + 2];
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = mode.IsKeyDefined
                                && buffer[bufIndex] == mode.KeyR
                                && buffer[bufIndex + 1] == mode.KeyG
                                && buffer[bufIndex + 2] == mode.KeyB ? (byte)0 : (byte)255;
                        }
                    }
                }
                else
                {
                    for (int i = 0; i != numPixels; ++i)
                    {
                        int bufIndex = numChannels * i;
                        buffer[bufIndex] = inBuffer[i * 6];
                        buffer[bufIndex + 1] = inBuffer[i * 6 + 2];
                        buffer[bufIndex + 2] = inBuffer[i * 6 + 4];
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = mode.IsKeyDefined
                                && 256U * inBuffer[i * 6] + inBuffer[i * 6 + 1] == mode.KeyR
                                && 256U * inBuffer[i * 6 + 2] + inBuffer[i * 6 + 3] == mode.KeyG
                                && 256U * inBuffer[i * 6 + 4] + inBuffer[i * 6 + 5] == mode.KeyB ? (byte)0 : (byte)255;
                        }
                    }
                }
            }
            else if (mode.ColorType == PNGColorType.Palette)
            {
                for (int i = 0; i != numPixels; ++i)
                {
                    int bufIndex = numChannels * i;
                    int index;
                    if (mode.BitDepth == 8)
                    {
                        index = inBuffer[i];
                    }
                    else
                    {
                        index = (int)BitHelper.ReadBitsFromReversedStream(ref i, inBuffer, (int)mode.BitDepth);
                    }

                    if (index >= mode.PaletteSize)
                    {
                        /*This is an error according to the PNG spec, but most PNG decoders make it black instead.
                        Done here too, slightly faster due to no error handling needed.*/
                        buffer[bufIndex] = buffer[bufIndex + 1] = buffer[bufIndex + 2] = 0;
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = 255;
                        }
                    }
                    else
                    {
                        buffer[bufIndex] = mode.Palette[index * 4];
                        buffer[bufIndex + 1] = mode.Palette[index * 4 + 1];
                        buffer[bufIndex + 2] = mode.Palette[index * 4 + 2];
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = mode.Palette[index * 4 + 3];
                        }
                    }
                }
            }
            else if (mode.ColorType == PNGColorType.GreyAlpha)
            {
                if (mode.BitDepth == 8)
                {
                    for (int i = 0; i != numPixels; ++i)
                    {
                        int bufIndex = numChannels * i;
                        buffer[bufIndex] = buffer[bufIndex + 1] = buffer[bufIndex + 2] = inBuffer[i * 2];
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = inBuffer[i * 2 + 1];
                        }
                    }
                }
                else
                {
                    for (int i = 0; i != numPixels; ++i)
                    {
                        int bufIndex = numChannels * i;
                        buffer[bufIndex] = buffer[bufIndex + 1] = buffer[bufIndex + 2] = inBuffer[i * 4];
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = inBuffer[i * 4 + 2];
                        }
                    }
                }
            }
            else if (mode.ColorType == PNGColorType.RGBA)
            {
                if (mode.BitDepth == 8)
                {
                    for (int i = 0; i != numPixels; ++i)
                    {
                        int bufIndex = numChannels * i;
                        buffer[bufIndex] = inBuffer[i * 4];
                        buffer[bufIndex + 1] = inBuffer[i * 4 + 1];
                        buffer[bufIndex + 2] = inBuffer[i * 4 + 2];
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = inBuffer[i * 4 + 3];
                        }
                    }
                }
                else
                {
                    for (int i = 0; i != numPixels; ++i)
                    {
                        int bufIndex = numChannels * i;
                        buffer[bufIndex] = inBuffer[i * 8];
                        buffer[bufIndex + 1] = inBuffer[i * 8 + 2];
                        buffer[bufIndex + 2] = inBuffer[i * 8 + 4];
                        if (hasAlpha)
                        {
                            buffer[bufIndex + 3] = inBuffer[i * 8 + 6];
                        }
                    }
                }
            }
        }

        /*Get RGBA8 color of pixel with index (y * width + x) from the raw image with given color type.*/
        private static unsafe void GetPixelColorRGBA8(ref byte r, ref byte g, ref byte b, ref byte a, byte[] inBuffer, int index, PNGColorMode mode)
        {
            if (mode.ColorType == PNGColorType.Grey)
            {
                if (mode.BitDepth == 8)
                {
                    r = g = b = a = inBuffer[index];
                    if (mode.IsKeyDefined && r == mode.KeyR)
                    {
                        a = 0;
                    }
                    else
                    {
                        a = 255;
                    }
                }
                else if (mode.BitDepth == 16)
                {
                    r = g = b = a = inBuffer[index * 2];
                    if (mode.IsKeyDefined
                        && 256U * inBuffer[index * 2] + inBuffer[index * 2 + 1] == mode.KeyR)
                    {
                        a = 0;
                    }
                    else
                    {
                        a = 255;
                    }
                }
                else
                {
                    /*highest possible value for this bit depth*/
                    var highest = ((1U << (int)mode.BitDepth) - 1U);
                    int j = (int)(index * mode.BitDepth);
                    var value = BitHelper.ReadBitsFromReversedStream(ref j, inBuffer, (int)mode.BitDepth);
                    r = g = b = (byte)((value * 255) / highest);
                    if (mode.IsKeyDefined && value == mode.KeyR)
                    {
                        a = 0;
                    }
                    else
                    {
                        a = 255;
                    }
                }
            }
            else if (mode.ColorType == PNGColorType.RGB)
            {
                if (mode.BitDepth == 8)
                {
                    r = inBuffer[index * 3];
                    g = inBuffer[index * 3 + 1];
                    b = inBuffer[index * 3 + 2];
                    if (mode.IsKeyDefined
                        && r == mode.KeyR 
                        && g == mode.KeyG
                        && b == mode.KeyB)
                    {
                        a = 0;
                    }
                    else
                    {
                        a = 255;
                    }
                }
                else
                {
                    r = inBuffer[index * 6];
                    g = inBuffer[index * 6 + 1];
                    b = inBuffer[index * 6 + 2];
                    if (mode.IsKeyDefined
                        && 256U * inBuffer[index * 6] + inBuffer[index * 6 + 1] == mode.KeyR
                        && 256U * inBuffer[index * 6 + 2] + inBuffer[index * 6 + 3] == mode.KeyG
                        && 256U * inBuffer[index * 6 + 4] + inBuffer[index * 6 + 5] == mode.KeyB)
                    {
                        a = 0;
                    }
                    else
                    {
                        a = 255;
                    }
                }
            }
            else if (mode.ColorType == PNGColorType.Palette)
            {
                int i = 0;
                if (mode.BitDepth == 8)
                {
                    i = inBuffer[index];
                }
                else
                {
                    int j = (int)(index * mode.BitDepth);
                    i = (int)BitHelper.ReadBitsFromReversedStream(ref j, inBuffer, (int)mode.BitDepth);
                }

                if ( i >= mode.PaletteSize)
                {
                    /*This is an error according to the PNG spec, but common PNG decoders make it black instead.
                    Done here too, slightly faster due to no error handling needed.*/
                    r = g = b = 0;
                    a = 255;
                }
                else
                {
                    r = mode.Palette[i * 4];
                    g = mode.Palette[i * 4 + 1];
                    b = mode.Palette[i * 4 + 2];
                    a = mode.Palette[i * 4 + 3];
                }
            }
            else if (mode.ColorType == PNGColorType.GreyAlpha)
            {
                if (mode.BitDepth == 8)
                {
                    r = g = b = inBuffer[index * 4];
                    a = inBuffer[index * 2 + 1];
                }
                else
                {
                    r = g = b = inBuffer[index * 4];
                    a = inBuffer[index * 4 + 2];
                }
            }
            else if (mode.ColorType == PNGColorType.RGBA)
            {
                if (mode.BitDepth == 8)
                {
                    r = inBuffer[index * 4];
                    g = inBuffer[index * 4 + 1];
                    b = inBuffer[index * 4 + 2];
                    a = inBuffer[index * 4 + 3];
                }
                else
                {
                    r = inBuffer[index * 8];
                    g = inBuffer[index * 8 + 2];
                    b = inBuffer[index * 8 + 4];
                    a = inBuffer[index * 8 + 6];
                }
            }
        }

        /*Put a pixel, given its RGBA color, into image of any color type*/
        private static uint RGBA8ToPixel(byte[] outBuffer, int index, PNGColorMode mode, 
            ref ColorTree tree /*for palette*/,
            byte r, byte g, byte b, byte a)
        {
            if (mode.ColorType == PNGColorType.Grey)
            {
                byte gray = r;
                if (mode.BitDepth == 8)
                {
                    outBuffer[index] = gray;
                }
                else if (mode.BitDepth == 16)
                {
                    outBuffer[index * 2] = outBuffer[index * 2 + 1] = gray;
                }
                else
                {
                    /*take the most significant bits of gray*/
                    gray = (byte)((gray >> (8 - (int)mode.BitDepth)) & ((1 << (int)mode.BitDepth) - 1));
                    AddColorBits(outBuffer, index, (int)mode.BitDepth, (int)gray);
                }
            }
            else if (mode.ColorType == PNGColorType.RGB)
            {
                if (mode.BitDepth == 8)
                {
                    outBuffer[index * 3] = r;
                    outBuffer[index * 3 + 1] = g;
                    outBuffer[index * 3 + 2] = b;
                }
                else
                {
                    outBuffer[index * 6] = outBuffer[index * 6 + 1] = r;
                    outBuffer[index * 6 + 2] = outBuffer[index * 6 + 3] = g;
                    outBuffer[index * 6 + 4] = outBuffer[index * 6 + 5] = b;
                }
            }
            else if (mode.ColorType == PNGColorType.Palette)
            {
                int i = ColorTree.Get(ref tree, r, g, b, a);
                if (i < 0)
                {
                    return 82;
                }

                if (mode.BitDepth == 8)
                {
                    outBuffer[index] = (byte)i;
                }
                else
                {
                    AddColorBits(outBuffer, index, (int)mode.BitDepth, i);
                }
            }
            else if (mode.ColorType == PNGColorType.GreyAlpha)
            {
                byte gray = r; /*((byte)r + g + b) / 3;*/
                if (mode.BitDepth == 8)
                {
                    outBuffer[index * 2] = gray;
                    outBuffer[index * 2 + 1] = a;
                }
                else if (mode.BitDepth == 16)
                {
                    outBuffer[index * 4] = outBuffer[index * 4 + 1] = gray;
                    outBuffer[index * 4 + 2] = outBuffer[index * 4 + 3] = a;
                }
            }
            else if (mode.ColorType == PNGColorType.RGBA)
            {
                if (mode.BitDepth == 8)
                {
                    outBuffer[index * 4] = r;
                    outBuffer[index * 4 + 1] = g;
                    outBuffer[index * 4 + 2] = b;
                    outBuffer[index * 4 + 3] = a;
                }
                else
                {
                    outBuffer[index * 8] = outBuffer[index * 8 + 1] = r;
                    outBuffer[index * 8 + 2] = outBuffer[index * 8 + 3]= g;
                    outBuffer[index * 8 + 4] = outBuffer[index * 8 + 5]= b;
                    outBuffer[index * 8 + 6] = outBuffer[index * 8 + 7] = a;
                }
            }

            return 0; //no errors
        }

        private static void RGBA16ToPixel(byte[] outBuffer, int index, PNGColorMode mode, ushort r, ushort g, ushort b, ushort a)
        {
            if (mode.ColorType == PNGColorType.Grey)
            {
                ushort gray = r;
                outBuffer[index * 2] = (byte)((gray >> 8) & 255);
                outBuffer[index * 2 + 1] = (byte)(gray & 255);
            }
            else if (mode.ColorType == PNGColorType.RGB)
            {
                outBuffer[index * 6] = (byte)((r >> 8) & 255);
                outBuffer[index * 6 + 1] = (byte)(r & 255);
                outBuffer[index * 6 + 2] = (byte)((g >> 8) & 255);
                outBuffer[index * 6 + 3] = (byte)(g & 255);
                outBuffer[index * 6 + 4] = (byte)((b >> 8) & 255);
                outBuffer[index * 6 + 5] = (byte)(b & 255);
            }
            else if (mode.ColorType == PNGColorType.GreyAlpha)
            {
                ushort gray = r;
                outBuffer[index * 4] = (byte)((gray >> 8) & 255);
                outBuffer[index * 4 + 1] = (byte)(gray & 255);
                outBuffer[index * 4 + 2] = (byte)((a >> 8) & 255);
                outBuffer[index * 4 + 3] = (byte)(a & 255);
            }
            else if (mode.ColorType == PNGColorType.RGBA)
            {
                outBuffer[index * 8] = (byte)((r >> 8) & 255);
                outBuffer[index * 8 + 1] = (byte)(r & 255);
                outBuffer[index * 8 + 2] = (byte)((g >> 8) & 255);
                outBuffer[index * 8 + 3] = (byte)(g & 255);
                outBuffer[index * 8 + 4] = (byte)((b >> 8) & 255);
                outBuffer[index * 8 + 5] = (byte)(b & 255);
                outBuffer[index * 8 + 6] = (byte)((a >> 8) & 255);
                outBuffer[index * 8 + 7] = (byte)(a & 255);
            }
        }

        /*index: bitgroup index, bits: 
         * bitgroup size(1, 2 or 4), 
         * input: bitgroup value, out: octet array to add bits to
         */
        private static void AddColorBits(byte[] outBuffer, int index, int bits, int input)
        {
            int m = bits == 1 ? 7 : bits == 2 ? 3 : 1; /*8 / bits -1 */
            /*p = the partial index in the byte, e.g. with 4 palettebits it is 0 for first half or 1 for second half*/
            int p = index & m;
            input &= ((1 << bits) - 1);
            input = input << (bits * (m - p));
            if (p == 0)
            {
                outBuffer[index * bits / 8] = (byte)input;
            }
            else
            {
                outBuffer[index * bits / 8] |= (byte)input;
            }
        }

    }
}
