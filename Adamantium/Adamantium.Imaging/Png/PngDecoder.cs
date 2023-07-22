using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Imaging.Png.Chunks;
using Adamantium.Imaging.Png.IO;

namespace Adamantium.Imaging.Png
{
    public class PngDecoder
    {
        private PNGStreamReader stream;

        public PngDecoder(PNGStreamReader stream)
        {
            this.stream = stream;
        }

        public IRawBitmap Decode(PngColorType colorType = PngColorType.RGBA, uint bitDepth = 8)
        {
            var state = new PngState();
            state.ColorModeRaw.ColorType = colorType;
            state.ColorModeRaw.BitDepth = bitDepth;
            return Decode(state);
        }

        private IRawBitmap Decode(PngState state)
        {
            var error = DecodeGeneric(state, out var pngImage);

            if (error != 0)
            {
                throw new PngDecodeException(error);
            }

            return pngImage;

            // TODO: should move these code to another place
            ////Premultiply alpha for formats, which does not support transparency
            //for (int i = 0; i < rawBuffer.Length; i+=4)
            //{
            //    float alpha = rawBuffer[i + 3] / 255.0f;

            //    rawBuffer[i] = (byte)(((rawBuffer[i]/255.0f) * alpha) * 255);
            //    rawBuffer[i + 1] = (byte)(((rawBuffer[i+1] / 255.0f) * alpha) * 255);
            //    rawBuffer[i + 2] = (byte)(((rawBuffer[i+2] / 255.0f) * alpha) * 255);
            //}
        }

        private ImageDescription GetImageDescription(PngState state, uint width, uint height)
        {
            var bitsPerPixel = PngColorConversion.GetBitsPerPixel(state.ColorModeRaw);
            ImageDescription descr = new ImageDescription();
            descr.Width = width;
            descr.Height = height;
            descr.ArraySize = 1;
            descr.MipLevels = 1;
            descr.Depth = 1;
            descr.Dimension = TextureDimension.Texture2D;
            descr.Format = bitsPerPixel switch
            {
                8 => AdamantiumVulkan.Core.Format.R8_UNORM,
                24 => AdamantiumVulkan.Core.Format.R8G8B8_UNORM,
                32 => AdamantiumVulkan.Core.Format.R8G8B8A8_UNORM,
                _ => descr.Format
            };

            return descr;
        }

        

        private uint DecodeGeneric(PngState state, out PngImage pngImage)
        {
            bool IEND = false;
            /*the data from idat chunks*/
            long predict = 0;

            // initialize out parameters in case of errors
            pngImage = new PngImage();
            pngImage.State = state;

            pngImage.Header = ReadHeaderChunk(state);
            pngImage.ColorType = state.ColorModeRaw.ColorType;
            if (state.Error != 0)
            {
                throw new PngDecodeException(state.Error);
            }

            if (CheckPixelOverflow(pngImage.Header.Width, pngImage.Header.Height, state.InfoPng.ColorMode, state.ColorModeRaw))
            {
                /*overflow possible due to amount of pixels*/
                state.Error = 92;
            }

            stream.Position = 33;

            long currentPosition = stream.Position;
            PngFrame currentFrame = null;

            while (!IEND && state.Error == 0)
            {
                /*error: size of the in buffer too small to contain next chunk*/
                if ((stream.Position+12)> stream.Length )
                {
                    if (state.DecoderSettings.IgnoreEnd)
                    {
                        /*other errors may still happen though*/
                        break;
                    }
                    state.Error = 30;
                }

                /*length of the data of the chunk, excluding the length bytes, chunk type and CRC bytes*/
                uint chunkSize = stream.ReadChunkSize();

                if (chunkSize > int.MaxValue)
                {
                    if (state.DecoderSettings.IgnoreEnd)
                    {
                        /*other errors may still happen though*/
                        break;
                    }
                    state.Error = 63;
                }

                if ((stream.Position + chunkSize + 12) > stream.Length)
                {
                    /*error: size of the in buffer too small to contain next chunk*/
                    state.Error = 64;
                }

                var chunkType = stream.ReadChunkType();

                var pos = stream.Position - 4;
                uint crc = 0;

                /*
                    If the default image is the first frame:

                    Sequence number    Chunk
                    (none)             `acTL`
                    0                  `fcTL` first frame
                    (none)             `IDAT` first frame / default image
                    1                  `fcTL` second frame
                    2                  first `fdAT` for second frame
                    3                  second `fdAT` for second frame
                    ....

                    If the default image is not part of the animation:
                                    Sequence number    Chunk
                    (none)             `acTL`
                    (none)             `IDAT` default image
                    0                  `fcTL` first frame
                    1                  first `fdAT` for first frame
                    2                  second `fdAT` for first frame
                    ....
                */
                bool isPartOfAnimation = false;
                switch (chunkType)
                {
                    case "acTL":
                        var actl = stream.ReadacTL(state);
                        pngImage.FramesCount = actl.FramesCount;
                        pngImage.RepeatCount = actl.RepeatCout;
                        break;
                    case "fcTL":
                        currentFrame = new PngFrame();
                        isPartOfAnimation = true;
                        pngImage.Frames.Add(currentFrame);
                        stream.ReadfcTL(state, currentFrame);
                        break;
                    case "fdAT":
                        currentFrame.SequenceNumberFDAT = stream.ReadUInt32();
                        currentFrame.FrameData = stream.ReadBytes((int)chunkSize - 4);
                        crc = stream.ReadUInt32();
                        stream.Position = pos;
                        var data = stream.ReadBytes(currentFrame.FrameData.Length + 8);
                        var checksum = Crc32.CalculateCheckSum(data);
                        if (crc != checksum && !state.DecoderSettings.IgnoreCrc)
                        {
                            state.Error = 57; // checksum mismatch;
                        }
                        break;
                    case "IDAT":
                        if (currentFrame == null && !isPartOfAnimation)
                        {
                            currentFrame = new PngFrame();
                            pngImage.DefaultImage = currentFrame;
                            pngImage.Frames.Add(currentFrame);
                        }
                        var bytes = stream.ReadBytes((int)chunkSize);
                        crc = stream.ReadUInt32();
                        stream.Position = pos;
                        var crcData = stream.ReadBytes(bytes.Length + 4);
                        checksum = Crc32.CalculateCheckSum(crcData);
                        if (crc != checksum && !state.DecoderSettings.IgnoreCrc)
                        {
                            state.Error = 57; // checksum mismatch;
                        }
                        currentFrame.AddBytes(bytes);
                        break;
                    case "IEND":
                        IEND = true;
                        if (state.Error == 64)
                        {
                            state.Error = 0;
                        }
                        break;
                    case "PLTE":
                        ReadPLTEChunk(state, chunkSize);
                        break;
                    case "tRNS":
                        ReadtRNSChunk(state, chunkSize);
                        break;
                    case "bkGD":
                        ReadbKGDChunk(state, chunkSize);
                        break;
                    case "tEXt":
                        ReadtEXtChunk(state, chunkSize);
                        break;
                    case "zTXt":
                        ReadzTXtChunk(state, this, chunkSize);
                        break;
                    case "iTXt":
                        ReadiTXtChunk(state, this, chunkSize);
                        break;
                    case "tIME":
                        ReadtIMEChunk(state);
                        break;
                    case "pHYs":
                        ReadpHYsChunk(state.InfoPng);
                        break;
                    case "gAMA":
                        ReadgAMAChunk(state.InfoPng);
                        break;
                    case "cHRM":
                        ReadcHRMChunk(state, chunkSize);
                        break;
                    case "sRGB":
                        ReadsRGBChunk(state.InfoPng);
                        break;
                    case "iCCP":
                        ReadiCCPChunk(state, chunkSize);
                        break;
                    /*it's not an implemented chunk type, so ignore it: skip over the data*/
                    default:
                        /*error: unknown critical chunk (5th bit of first byte of chunk type is 0)*/
                        if (!state.DecoderSettings.IgnoreCritical)
                        {
                            state.Error = 69;
                        }
                        break;
                }

                if (!IEND)
                {
                    currentPosition += chunkSize + 12;
                    stream.Position = currentPosition;
                }
            }

            return state.Error;
        }

        private void ReadPLTEChunk(PngState state, uint chunkSize)
        {
            stream.ReadPLTE(state, chunkSize);
        }

        private void ReadtRNSChunk(PngState state, uint chunkSize)
        {
            stream.ReadtRNS(state, chunkSize);
        }

        private void ReadbKGDChunk(PngState state, uint chunkSize)
        {
            stream.ReadbKGD(state, chunkSize);
        }

        private void ReadzTXtChunk(PngState state, PngDecoder pNGDecoder, uint chunkSize)
        {
            stream.ReadzTXt(state, pNGDecoder, chunkSize);
        }

        private void ReadtIMEChunk(PngState state)
        {
            stream.ReadtIME(state);
        }

        private void ReadiTXtChunk(PngState state, PngDecoder pNGDecoder, uint chunkSize)
        {
            stream.ReadiTXt(state, pNGDecoder, chunkSize);
        }

        private void ReadcHRMChunk(PngState state, uint chunkSize)
        {
            if (chunkSize != 32)
            {
                /*invalid cHRM chunk size*/
                state.Error = 97;
                return;
            }
            state.InfoPng.cHRM = stream.ReadcHRM();
        }

        /*in an idat chunk, each scanline is a multiple of 8 bits, unlike the lodepng output buffer,
        and in addition has one extra byte per line: the filter byte. So this gives a larger
        result than lodepng_get_raw_size. */
        internal static long GetRawSizeIdat(int width, int height, PngColorMode colorMode)
        {
            var bpp = PngColorConversion.GetBitsPerPixel(colorMode);
            /* + 1 for the filter byte, and possibly plus padding bits per line */
            var line = ((width / 8) * bpp) + 1 + ((width & 7) * bpp + 7) / 8;
            return height * line;
        }

        internal static long GetRawSizeLct(int width, int height, PngColorMode colorMode)
        {
            var bpp = PngColorConversion.GetBitsPerPixel(colorMode);
            var n = width * height;
            return ((n / 8) * bpp) + ((n & 7) * bpp + 7) / 8;
        }

        private void ReadtEXtChunk(PngState state, uint chunkLength)
        {
            if (!state.DecoderSettings.ReadTextChunks)
            {
                return;
            }
            var text = stream.ReadtEXt(state, chunkLength);
            var textItem = new TXTItem();
            textItem.Key = text.Key;
            textItem.Text = text.Text;
            state.InfoPng.TextItems.Add(textItem);
        }

        /*reads header and resets other parameters in state->info_png*/
        private IHDR ReadHeaderChunk(PngState state)
        {
            var info = state.InfoPng;

            if (stream.Length == 0)
            {
                /*error: the given data is empty*/
                state.Error = 48;
            }

            if (stream.Length < 33)
            {
                /*error: the data length is smaller than the length of a PNG header*/
                state.Error = 27;
            }

            if (!stream.ReadPNGSignature())
            {
                /*error: the first 8 bytes are not the correct PNG signature*/
                state.Error = 28;
            }

            if (stream.ReadInt32() != 13)
            {
                /*error: header size must be 13 bytes*/
                state.Error = 94;
            }

            //stream.Position -= 4;

            if (stream.ReadChunkType() != "IHDR")
            {
                /*error: it doesn't start with a IHDR chunk!*/
                state.Error = 29;
            }
            var header = stream.ReadIHDR();

            info.ColorMode.BitDepth = header.BitDepth;
            info.ColorMode.ColorType = header.ColorType;
            info.CompressionMethod = header.CompressionMethod;
            info.FilterMethod = header.FilterMethod;
            info.InterlaceMethod = header.InterlaceMethod;

            if (!state.DecoderSettings.IgnoreCrc)
            {
                if (header.CRC != header.CheckSum)
                {
                    /*invalid CRC*/
                    state.Error = 57;
                }
            }

            /*error: only compression method 0 is allowed in the specification*/
            if (info.CompressionMethod != 0) state.Error = 32;
            /*error: only filter method 0 is allowed in the specification*/
            if (info.FilterMethod != 0) state.Error = 33;

            state.Error = CheckColorValidity(info.ColorMode.ColorType, info.ColorMode.BitDepth);

            return header;
        }

        private void ReadiCCPChunk(PngState state, uint chunkSize)
        {
            var iccp = stream.ReadiCCP(state, this, chunkSize);
        }

        private void ReadsRGBChunk(PngInfo info)
        {
            var srgb = stream.ReadsRGB();
            info.IsSrgbDefined = true;
            info.SrgbIntent = srgb.RenderingIntent;
        }

        private void ReadgAMAChunk(PngInfo info)
        {
            var gama = stream.ReadgAMA();
            info.IsGamaDefined = true;
            info.Gamma = gama.Gamma;
        }

        private void ReadpHYsChunk(PngInfo info)
        {
            var phys = stream.ReadpHYs();
            info.PhysX = phys.PhysX;
            info.PhysY = phys.PhysY;
            info.PhysUnit = phys.Unit;
            info.IsPhysDefined = true;
        }

        private uint CheckColorValidity(PngColorType colorType, uint bitDepth)
        {
            switch(colorType)
            {
                case PngColorType.Grey:
                    if (!(bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8|| bitDepth == 16))
                    {
                        return 37;
                    }
                    break;
                case PngColorType.GreyAlpha:
                case PngColorType.RGB:
                case PngColorType.RGBA:
                    if (!(bitDepth == 8 || bitDepth == 16))
                    {
                        return 37;
                    }
                    break;
                case PngColorType.Palette:
                    if (!(bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8))
                    {
                        return 37;
                    }
                    break;
                default:
                    return 31;
            }
            return 0;
        }

        /*Safely checks whether size_t overflow can be caused due to amount of pixels.
        This check is overcautious rather than precise. If this check indicates no overflow,
        you can safely compute in a size_t (but not an unsigned):
            -(size_t)w * (size_t)h * 8
            -amount of bytes in IDAT (including filter, padding and Adam7 bytes)
            -amount of bytes in raw color model
        Returns true if overflow possible, false if not.
        */
        private bool CheckPixelOverflow(int width, int height, PngColorMode pngColor, PngColorMode rawColor)
        {
            ulong bpp = Math.Max(PngColorConversion.GetBitsPerPixel(pngColor), PngColorConversion.GetBitsPerPixel(rawColor));
            ulong numPixels, total;
            ulong line; // bytes per line in worst case

            if (MulOverflow((ulong)width, (ulong)height, out numPixels))
            {
                return true;
            }

            /* bit pointer with 8-bit color, or 8 bytes per channel color */
            if (MulOverflow(numPixels, 8, out total))
            {
                return true;
            }

            /* Bytes per scanline with the expression "(width / 8) * bpp) + ((width & 7) * bpp + 7) / 8" */
            if (MulOverflow((ulong)(width / 8), bpp, out line))
            {
                return true;
            }

            if (AddOverflow(line, ((ulong)(width & 7) * bpp +7) / 8, out line))
            {
                return true;
            }

            /* 5 bytes overhead per line: 1 filterbyte, 4 for Adam7 worst case */
            if (AddOverflow(line, 5, out line))
            {
                return true;
            }

            /* Total bytes in worst case */
            if (AddOverflow(line, (ulong)height, out total))
            {
                return true;
            }

            return false;
        }

        /* Safely check if multiplying two integers will overflow (no undefined
        behavior, compiler removing the code, etc...) and output result. */
        private bool MulOverflow(ulong a, ulong b, out ulong result)
        {
            result = a * b;
            return (a != 0 && result / a != b);
        }

        /* Safely check if adding two integers will overflow (no undefined
        behavior, compiler removing the code, etc...) and output result. */
        private bool AddOverflow(ulong a, ulong b, out ulong result)
        {
            result = a + b;
            return result < a;
        }
    }
}
