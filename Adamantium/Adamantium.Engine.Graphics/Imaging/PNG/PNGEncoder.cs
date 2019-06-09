using Adamantium.Engine.Graphics.Imaging.PNG.Chunks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class PNGEncoder
    {
        private PNGCompressor compressor;
        private Stream memoryStream;
        private PNGStream pngStream;
        public PNGEncoder(Stream memoryStream)
        {
            compressor = new PNGCompressor();
            this.memoryStream = memoryStream;
            //pngStream = new PNGStream();
        }

        public uint Encode(PixelBuffer[] pixelBuffers, PNGState state)
        {
            uint error = 0;
            

            /*check input values validity*/
            if ((state.InfoPng.ColorMode.ColorType == PNGColorType.Palette || state.EncoderSettings.ForcePalette)
                && (state.InfoPng.ColorMode.PaletteSize == 0 || state.InfoPng.ColorMode.PaletteSize > 256))
            {
                /*invalid palette size, it is only allowed to be 1-256*/
                return 68;
            }

            if (state.EncoderSettings.BType > 2)
            {
                /*error: unexisting btype*/
                return 61;
            }

            state.Error = PNGColorConvertion.CheckColorValidity(state.InfoPng.ColorMode.ColorType, state.InfoPng.ColorMode.BitDepth);
            if (state.Error > 0)
            {
                return state.Error;
            }

            state.Error = PNGColorConvertion.CheckColorValidity(state.InfoRaw.ColorType, state.InfoRaw.BitDepth);
            if (state.Error > 0)
            {
                return state.Error;
            }

            /* color convert and compute scanline filter types */
            PNGInfo info = new PNGInfo(state.InfoPng);
            PNGImage pngImage = PNGImage.FromPixelBuffers(pixelBuffers);

            if (state.EncoderSettings.AutoConvert)
            {
                if (state.InfoPng.IsBackgroundDefined)
                {
                    var bgR = state.InfoPng.BackgroundR;
                    var bgG = state.InfoPng.BackgroundG;
                    var bgB = state.InfoPng.BackgroundB;
                    uint r = 0;
                    uint g = 0;
                    uint b = 0;

                    PNGColorProfile profile = new PNGColorProfile();
                    PNGColorMode mode16 = PNGColorMode.Create(PNGColorType.RGB, 16);
                    PNGColorConvertion.ConvertRGB(ref r, ref g, ref b, bgR, bgG, bgB, mode16, state.InfoPng.ColorMode);
                    var frame = pngImage.Frames[0];
                    PNGColorConvertion.GetColorProfile(profile, frame.RawPixelBuffer, (uint)frame.Width, (uint)frame.Height, state.InfoRaw);
                    profile.Add(r, g, b, ushort.MaxValue);
                    PNGColorProfile.AutoChooseColorFromProfile(info.ColorMode, state.InfoRaw, profile);
                    error = PNGColorConvertion.ConvertRGB(ref info.BackgroundR, ref info.BackgroundG,
                        ref info.BackgroundB, bgR, bgG, bgB, info.ColorMode, state.InfoPng.ColorMode);
                    if (error > 0)
                    {
                        throw new PNGEncoderException(error);
                    }
                }
                else
                {
                    var frame = pngImage.Frames[0];
                    PNGColorProfile.AutoChooseColor(info.ColorMode, frame.RawPixelBuffer, (uint)frame.Width, (uint)frame.Height, state.InfoRaw);
                }
            }

            if (state.InfoPng.IsIccpDefined)
            {
                var grayICC = iCCP.IsGrayICCProfile(state.InfoPng.IccpProfile);
                var grayPng = info.ColorMode.ColorType == PNGColorType.Grey || info.ColorMode.ColorType == PNGColorType.GreyAlpha;
                /* TODO: perhaps instead of giving errors or less optimal compression, we can automatically modify
                the ICC profile here to say "GRAY" or "RGB " to match the PNG color type, unless this will require
                non trivial changes to the rest of the ICC profile */
                if (!grayICC && !iCCP.IsRGBICCProfile(state.InfoPng.IccpProfile))
                {
                    /* Disallowed profile color type for PNG */
                    throw new PNGEncoderException(100);
                }
                if (!state.EncoderSettings.AutoConvert && grayICC != grayPng)
                {
                    /* Non recoverable: encoder not allowed to convert color type, and requested color type not
                    compatible with ICC color type */
                    throw new PNGEncoderException(101);
                }
                if (grayICC && !grayPng)
                {
                    /* Non recoverable: trying to set grayscale ICC profile while colored pixels were given */
                    throw new PNGEncoderException(102);
                    /* NOTE: this relies on the fact that PNGColorProfile.AutoChooseColor never returns palette for grayscale pixels */
                }
                if (!grayICC && grayPng)
                {
                    /* Recoverable but an unfortunate loss in compression density: We have grayscale pixels but
                    are forced to store them in more expensive RGB format that will repeat each value 3 times
                    because the PNG spec does not allow an RGB ICC profile with internal grayscale color data */
                    if (info.ColorMode.ColorType == PNGColorType.Grey) info.ColorMode.ColorType = PNGColorType.RGB;
                    if (info.ColorMode.ColorType == PNGColorType.GreyAlpha) info.ColorMode.ColorType = PNGColorType.RGBA;
                    if (info.ColorMode.BitDepth < 8) info.ColorMode.BitDepth = 8;
                }
            }

            if (state.InfoPng.ColorMode != info.ColorMode)
            {
                foreach (PNGFrame frame in pngImage.Frames)
                {
                    long size = frame.Width * frame.Height * PNGColorConvertion.GetBitsPerPixel(info.ColorMode) + 7 / 8;
                    var converted = new byte[size];
                    state.Error = PNGColorConvertion.Convert(converted, frame.RawPixelBuffer, info.ColorMode, state.InfoRaw, frame.Width, frame.Height);
                    if (state.Error > 0)
                    {
                        throw new PNGEncoderException(state.Error);
                    }
                    var compressedBuffer = new byte[0];
                    state.Error = PresprocessScanlines(ref compressedBuffer, converted, (uint)frame.Width, (uint)frame.Height, info, state.EncoderSettings);
                    frame.CompressedPixelBuffer = compressedBuffer;
                    if (state.Error > 0)
                    {
                        throw new PNGEncoderException(state.Error);
                    }
                }
            }
            else
            {
                foreach (PNGFrame frame in pngImage.Frames)
                {
                    var compressedBuffer = new byte[0];
                    state.Error = PresprocessScanlines(ref compressedBuffer, frame.RawPixelBuffer, (uint)frame.Width, (uint)frame.Height, info, state.EncoderSettings);
                    frame.CompressedPixelBuffer = compressedBuffer;
                    if (state.Error > 0)
                    {
                        throw new PNGEncoderException(state.Error);
                    }
                }
            }

            return error;
        }



        private unsafe void AddPaddingBits(byte[] outData, byte* inData, long olineBits, long ilineBits, uint height)
        {
            /*The opposite of the removePaddingBits function
            olinebits must be >= ilinebits*/
            var diff = olineBits - ilineBits;
            /*bit pointers*/
            int obp = 0;
            int ibp = 0;
            for (int y = 0; y != height; ++y)
            {
                for (int x = 0; x < ilineBits; ++x)
                {
                    byte bit = BitHelper.ReadBitFromReversedStream(ref ibp, inData);
                    BitHelper.SetBitOfReversedStream(ref obp, outData, bit);
                }
                /*obp += diff; --> no, fill in some value in the padding bits too, to avoid
                "Use of uninitialised value of size ###" warning from valgrind*/
                for (int x = 0; x != diff; ++x)
                {
                    BitHelper.SetBitOfReversedStream(ref obp, outData, 0);
                }
            }
        }

        /*
        This function converts the pure 2D image with the PNG's colortype, into filtered-padded-interlaced data. Steps:
        *) if no Adam7: 1) add padding bits (= possible extra bits per scanline if bpp < 8) 2) filter
        *) if adam7: 1) Adam7_interlace 2) 7x add padding bits 3) 7x filter
        */
        /*out must be buffer big enough to contain uncompressed IDAT chunk data, and in must contain the full image.
        return value is error**/
        private unsafe uint PresprocessScanlines(ref byte[] outData, byte[] inData, uint width, uint height, PNGInfo pngInfo, PNGEncoderSettings settings)
        {
            uint error = 0;
            var bpp = PNGColorConvertion.GetBitsPerPixel(pngInfo.ColorMode);

            if (pngInfo.InterlaceMethod == InterlaceMethod.None)
            {
                /*image size plus an extra byte per scanline + possible padding bits*/
                var outSize = height + (height * ((width * bpp + 7) / 8));
                outData = new byte[outSize];

                /*non multiple of 8 bits per scanline, padding bits needed per scanline*/
                if (bpp < 8 && width * bpp != ((width * bpp + 7) / 8) * 8)
                {
                    byte[] padded = new byte[height * ((width * bpp + 7) / 8)];
                    fixed (byte* inPtr = &inData[0])
                    {
                        AddPaddingBits(padded, inPtr, ((width * bpp + 7) / 8) * 8, (width * bpp), height);
                    }
                    fixed (byte* paddedPtr = &padded[0])
                    {
                        fixed (byte* inPtr = &inData[0])
                        {
                            error = PNGFilter.Filter(paddedPtr, inPtr, width, height, pngInfo.ColorMode, settings);
                        }
                    }
                }
                else
                {
                    /*we can immediately filter into the out buffer, no other steps needed*/
                    fixed (byte* outPtr = &outData[0])
                    {
                        fixed (byte* inPtr = &inData[0])
                        {
                            error = PNGFilter.Filter(outPtr, inPtr, width, height, pngInfo.ColorMode, settings);
                        }
                    }
                }
            }
            else
            {
                uint[] passWidth = new uint[7];
                uint[] passHeight = new uint[7];
                uint[] filterPassStart = new uint[8];
                uint[] paddedPassStart = new uint[8];
                uint[] passStart = new uint[8];
                byte[] adam7;

                Adam7.GetPassValues(passWidth, passHeight, filterPassStart, paddedPassStart, passStart, width, height, bpp);

                var outSize = filterPassStart[7]; /*image size plus an extra byte per scanline + possible padding bits*/
                outData = new byte[outSize];

                adam7 = new byte[passStart[7]];

                Adam7.Interlace(adam7, inData, width, height, bpp);
                for (int i = 0; i != 7; ++i)
                {
                    if (bpp < 8)
                    {
                        byte[] padded = new byte[paddedPassStart[i + 1] - paddedPassStart[i]];

                        fixed (byte* adam7Ptr = &adam7[passStart[i]])
                        {
                            var olineBits = ((passWidth[i] * bpp + 7) / 8) * 8;
                            var ilineBits = passWidth[i] * bpp;
                            AddPaddingBits(padded, adam7Ptr, olineBits, ilineBits, passHeight[i]);
                        }

                        fixed (byte* outPtr = &outData[filterPassStart[i]])
                        {
                            fixed (byte* paddedPtr = &padded[0])
                            {
                                error = PNGFilter.Filter(outPtr, paddedPtr, width, height, pngInfo.ColorMode, settings);
                            }
                        }
                    }
                    else
                    {
                        fixed (byte* outPtr = &outData[filterPassStart[i]])
                        {
                            fixed (byte* paddedPtr = &adam7[paddedPassStart[i]])
                            {
                                error = PNGFilter.Filter(outPtr, paddedPtr, passWidth[i], passHeight[i], pngInfo.ColorMode, settings);
                            }
                        }
                    }

                    if (error > 0) break; 
                }
            }

            return error;
        }

        /*
        palette must have 4 * palettesize bytes allocated, and given in format RGBARGBARGBARGBA...
        returns 0 if the palette is opaque,
        returns 1 if the palette has a single color with alpha 0 ==> color key
        returns 2 if the palette is semi-translucent.
        */
        private PaletteTranslucency GetPaletteTranslucency(byte[] palette)
        {
            byte key = 0;
            /*the value of the color with alpha 0, so long as color keying is possible*/
            byte r = 0;
            byte g = 0;
            byte b = 0;

            for (int i = 0; i != palette.Length; ++i)
            {
                if (key == 0 && palette[4 * i + 3] == 0)
                {
                    r = palette[4 * i];
                    g = palette[4 * i + 1];
                    b = palette[4 * i + 2];
                    key = 1;
                    i = -1; /*restart from beginning, to detect earlier opaque colors with key's value*/
                }
                else if (palette[4 * i + 3] != 255)
                {
                    key = 2;
                    break;
                }
                /*when key, no opaque RGB may have key's RGB*/
                else if (key != 0 
                    && r == palette[i * 4]
                    && g == palette[i * 4 + 1]
                    && b == palette[i * 4 + 2])
                {
                    key = 2;
                    break;
                }
            }

            return (PaletteTranslucency)key;
        }

        enum PaletteTranslucency : byte
        {
            Opaque = 0,
            ColorKey = 1,
            SemiTranslucent = 2
        }
    }
}
