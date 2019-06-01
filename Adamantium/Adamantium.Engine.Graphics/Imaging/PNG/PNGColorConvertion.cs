using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class PNGColorConvertion
    {
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
                            buffer[bufIndex + 3] = (byte)(mode.IsKeyDefined && 256U * inBuffer[i * 2] + inBuffer[i * 2 +1] == mode.KeyR ? 0 : 255);
                        }
                    }
                }
                else
                {
                    /*highest possible value for this bit depth*/
                    var highest = ((1U << (int)mode.BitDepth) - 1U);
                    fixed (byte* inPtr = &inBuffer[0])
                    {
                        for (int i = 0; i != numPixels; ++i)
                        {
                            int bufIndex = numChannels * i;
                            var value = PNGDecoder.ReadBitsFromReversedStream(ref i, inPtr, (int)mode.BitDepth);
                            buffer[bufIndex] = buffer[bufIndex + 1] = buffer[bufIndex + 2] = (byte)((value * 255) / highest);
                            if (hasAlpha)
                            {
                                buffer[bufIndex + 3] = (byte)(mode.IsKeyDefined && value == mode.KeyR ? 0 : 255);
                            }
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
                int index = 0;
                fixed (byte* inPtr = &inBuffer[0])
                {
                    for (int i = 0; i != numPixels; ++i)
                    {
                        int bufIndex = numChannels * i;
                        if (mode.BitDepth == 8)
                        {
                            index = inBuffer[i];
                        }
                        else
                        {
                            index = (int)PNGDecoder.ReadBitsFromReversedStream(ref i, inPtr, (int)mode.BitDepth);
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
                    fixed (byte* inPtr = &inBuffer[0])
                    {
                        var value = PNGDecoder.ReadBitsFromReversedStream(ref j, inPtr, (int)mode.BitDepth);
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
                    fixed (byte* inPtr = &inBuffer[0])
                    {
                        i = (int)PNGDecoder.ReadBitsFromReversedStream(ref j, inPtr, (int)mode.BitDepth);
                    }
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
