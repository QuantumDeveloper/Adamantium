using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public class PNGDecoder
    {
        private PNGStreamReader stream;
        private PNGCompressor compressor;
        public PNGDecoder(PNGStreamReader stream)
        {
            this.stream = stream;
            compressor = new PNGCompressor();
        }

        public Image Decode()
        {
            return Decode(PNGColorType.RGBA, 8);
        }

        private Image Decode(PNGColorType colorType = PNGColorType.RGBA, uint bitDepth = 8)
        {
            PNGState state = new PNGState();
            state.InfoRaw.ColorType = colorType;
            state.InfoRaw.BitDepth = bitDepth;
            return Decode(state);
        }

        private Image Decode(PNGState state)
        {
            var error = DecodeGeneric(state, out var outBuffer, out int width, out int height);

            if (error != 0)
            {
                throw new PNGDecodeException(error.ToString());
            }

            byte[] rawBuffer = null;
            if (!state.DecoderSettings.ColorСonvert || state.InfoRaw == state.InfoPng.ColorMode)
            {
                /*same color type, no copying or converting of data needed*/
                /*store the info_png color settings on the info_raw so that the info_raw still reflects what colortype
                the raw image has to the end user*/
                if (!state.DecoderSettings.ColorСonvert)
                {
                    state.InfoRaw = state.InfoPng.ColorMode;
                }
                rawBuffer = outBuffer;
            }
            else
            {
                /*color conversion needed; sort of copy of the data*/
                if (!(state.InfoRaw.ColorType == PNGColorType.RGB || state.InfoRaw.ColorType == PNGColorType.RGBA)
                    && state.InfoRaw.BitDepth != 8)
                {
                    /*unsupported color mode conversion*/
                    error = 56;
                    throw new PNGDecodeException(error.ToString());
                }

                int rawBufferSize = (int)GetRawSizeLct(width, height, state.InfoRaw);
                rawBuffer = new byte[rawBufferSize];

                error = PNGColorConvertion.Convert(rawBuffer, outBuffer, state.InfoRaw, state.InfoPng.ColorMode, width, height);

                if (error > 0)
                {
                    throw new PNGDecodeException(error.ToString());
                }
            }

            var bitsPerPixel = PNGColorConvertion.GetBitsPerPixel(state.InfoRaw);
            ImageDescription descr = new ImageDescription();
            descr.Width = width;
            descr.Height = height;
            descr.ArraySize = 1;
            descr.MipLevels = 1;
            descr.Depth = 1;
            descr.Dimension = TextureDimension.Texture2D;
            if (bitsPerPixel == 24)
            {
                descr.Format = AdamantiumVulkan.Core.Format.R8G8B8_UNORM;
            }
            else if (bitsPerPixel == 32)
            {
                descr.Format = AdamantiumVulkan.Core.Format.R8G8B8A8_UNORM;
            }

            //Premultiply alpha for formats, which does not support tansparency
            for (int i = 0; i < rawBuffer.Length; i+=4)
            {
                float alpha = rawBuffer[i + 3] / 255.0f;

                rawBuffer[i] = (byte)(((rawBuffer[i]/255.0f) * alpha) * 255);
                rawBuffer[i + 1] = (byte)(((rawBuffer[i+1] / 255.0f) * alpha) * 255);
                rawBuffer[i + 2] = (byte)(((rawBuffer[i+2] / 255.0f) * alpha) * 255);
            }

            var img = Image.New(descr);
            var handle = GCHandle.Alloc(rawBuffer, GCHandleType.Pinned);
            Utilities.CopyMemory(img.DataPointer, handle.AddrOfPinnedObject(), img.PixelBuffer[0].BufferStride);
            handle.Free();

            return img;
        }

        private uint DecodeGeneric(PNGState state, out byte[] outBuffer, out int width, out int height)
        {
            bool IEND = false;
            string chunk;
            ulong i;
            /*the data from idat chunks*/
            List<byte> idat = new List<byte>();
            long predict = 0;

            /*1 = after IHDR, 2 = after PLTE, 3 = after IDAT*/
            uint criticalPos = 1;

            // initialize out parameters in case of errors
            outBuffer = new byte[0];
            width = height = 0;

            ReadHeaderChunk(state, out width, out height);
            if (state.Error != 0)
            {
                throw new PNGDecodeException("");
            }

            if (CheckPixelOverflow(width, height, state.InfoPng.ColorMode, state.InfoRaw))
            {
                /*overflow possible due to amount of pixels*/
                state.Error = 92;
            }

            stream.Position = 33;

            long currentPosition = stream.Position;

            while (!IEND && state.Error == 0)
            {
                uint chunkSize = 0;

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
                chunkSize = stream.ReadChunkSize();

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

                switch (chunkType)
                {
                    case "IDAT":
                        var pos = stream.Position - 4;
                        var bytes = stream.ReadBytes((int)chunkSize);
                        var crc = stream.ReadUInt32();
                        stream.Position = pos;
                        var data = stream.ReadBytes(bytes.Length + 4);
                        var checksum = CRC32.CalculateCheckSum(data);
                        if (crc != checksum)
                        {
                            state.Error = 57; // checksum mismatch;
                        }
                        idat.AddRange(bytes);
                        break;
                    case "IEND":
                        IEND = true;
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

            predict = 0;
            if (state.InfoPng.InterlaceMethod == InterlaceMethod.None)
            {
                predict = GetRawSizeIdat(width, height, state.InfoPng.ColorMode);
            }
            else
            {
                /*Adam-7 interlaced: predicted size is the sum of the 7 sub-images sizes*/
                var colorMode = state.InfoPng.ColorMode;
                predict += GetRawSizeIdat((width + 7) >> 3 , (height + 7) >> 3, colorMode);
                if (width > 4)
                {
                    predict += GetRawSizeIdat((width + 3) >> 3, (height + 7) >> 3, colorMode);
                }
                predict += GetRawSizeIdat((width + 3) >> 2, (height + 3) >> 3, colorMode);
                if (width > 2)
                {
                    predict += GetRawSizeIdat((width + 1) >> 2, (height + 3) >> 2, colorMode);
                }
                predict += GetRawSizeIdat((width + 1) >> 1, (height + 1) >> 2, colorMode);
                if (width > 1)
                {
                    predict += GetRawSizeIdat((width) >> 1, (height + 1) >> 1, colorMode);
                }
                predict += GetRawSizeIdat((width), (height) >> 1, colorMode);
            }
            var scanlines = new List<byte>((int)predict);
            var error = compressor.Decompress(idat.ToArray(), state.DecoderSettings, scanlines);

            long bufferSize = 0;

            bufferSize = GetRawSizeLct(width, height, state.InfoPng.ColorMode);
            outBuffer = new byte[bufferSize];

            if (error == 0)
            {
                state.Error = PostProcessScanline(outBuffer, scanlines.ToArray(), width, height, state.InfoPng);
            }

            return error;
        }

        private void ReadPLTEChunk(PNGState state, uint chunkSize)
        {
            stream.ReadPLTE(state, chunkSize);
        }

        private void ReadtRNSChunk(PNGState state, uint chunkSize)
        {
            stream.ReadtRNS(state, chunkSize);
        }

        private void ReadbKGDChunk(PNGState state, uint chunkSize)
        {
            stream.ReadbKGD(state, chunkSize);
        }

        private void ReadzTXtChunk(PNGState state, PNGDecoder pNGDecoder, uint chunkSize)
        {
            stream.ReadzTXt(state, pNGDecoder, chunkSize);
        }

        private void ReadtIMEChunk(PNGState state)
        {
            stream.ReadtIME(state);
        }

        private void ReadiTXtChunk(PNGState state, PNGDecoder pNGDecoder, uint chunkSize)
        {
            stream.ReadiTXt(state, pNGDecoder, chunkSize);
        }

        private void ReadcHRMChunk(PNGState state, uint chunkSize)
        {
            if (chunkSize != 32)
            {
                /*invalid cHRM chunk size*/
                state.Error = 97;
                return;
            }
            state.InfoPng.cHRM = stream.ReadcHRM();
        }

        

        private unsafe uint PostProcessScanline(byte[] rawBuffer, byte[] inputData, int width, int height, PNGInfo infoPng)
        {
            /*
            This function converts the filtered-padded-interlaced data into pure 2D image buffer with the PNG's colortype.
            Steps:
            *) if no Adam7: 1) unfilter 2) remove padding bits (= posible extra bits per scanline if bpp < 8)
            *) if adam7: 1) 7x unfilter 2) 7x remove padding bits 3) Adam7_deinterlace
            NOTE: the in buffer will be overwritten with intermediate data!
            */
            uint error = 0;
            var bpp = PNGColorConvertion.GetBitsPerPixel(infoPng.ColorMode);
            if (bpp == 0)
            {
                /*error: invalid colortype*/
                return 31;
            }

            if (infoPng.InterlaceMethod == 0)
            {
                fixed (byte* inPtr = &inputData[0])
                {
                    fixed (byte* rawPtr = &rawBuffer[0])
                    {
                        if (bpp < 8 && width * bpp != ((width * bpp + 7) / 8) * 8)
                        {
                            error = PNGFilter.Unfilter(inPtr, inPtr, width, height, (int)bpp);
                            if (error > 0)
                            {
                                return error;
                            }
                            RemovePaddingBits(rawPtr, inPtr, (uint)(width * bpp), (uint)((width * bpp + 7) / 8) * 8, (uint)height);
                        }
                        else
                        {
                            error = PNGFilter.Unfilter(rawPtr, inPtr, width, height, (int)bpp);
                        }
                    }
                }
            }
            else /*interlace_method is 1 (Adam7)*/
            {
                uint[] passWidth = new uint[7];
                uint[] passHeight = new uint[7];
                uint[] filterPassStart = new uint[8];
                uint[] paddedPassStart = new uint[8];
                uint[] passStart = new uint[8];

                Adam7.GetPassValues(passWidth, passHeight, filterPassStart, paddedPassStart, passStart, (uint)width, (uint)height, bpp);

                for (int i = 0; i != 7; ++i)
                {
                    fixed (byte* rawPtr = &inputData[paddedPassStart[i]])
                    {
                        fixed (byte* inPtr = &inputData[filterPassStart[i]])
                        {
                            error = PNGFilter.Unfilter(rawPtr, inPtr, (int)passWidth[i], (int)passHeight[i], (int)bpp);
                        }
                    }

                    /*TODO: possible efficiency improvement: if in this reduced image the bits fit nicely in 1 scanline,
                    move bytes instead of bits or move not at all*/
                    if (bpp < 8)
                    {
                        /*remove padding bits in scanlines; after this there still may be padding
                        bits between the different reduced images: each reduced image still starts nicely at a byte*/
                        fixed (byte* rawPtr = &inputData[passStart[i]])
                        {
                            fixed (byte* inPtr = &inputData[paddedPassStart[i]])
                            {
                                RemovePaddingBits(rawPtr, inPtr, passWidth[i] * bpp, 
                                    ((passWidth[i] * bpp + 7) / 8) * 8, passHeight[i]);
                            }
                        }
                    }
                }

                Adam7.Deinterlace(rawBuffer, inputData, (uint)width, (uint)height, bpp);
            }

            return error;
        }

        private unsafe void RemovePaddingBits(byte* rawBuffer, byte* inputData, uint olinebits, uint ilinebits, uint height)
        {
            /*
            After filtering there are still padding bits if scanlines have non multiple of 8 bit amounts. They need
            to be removed (except at last scanline of (Adam7-reduced) image) before working with pure image buffers
            for the Adam7 code, the color convert code and the output to the user.
            in and out are allowed to be the same buffer, in may also be higher but still overlapping; in must
            have >= ilinebits*h bits, out must have >= olinebits*h bits, olinebits must be <= ilinebits
            also used to move bits after earlier such operations happened, e.g. in a sequence of reduced images from Adam7
            only useful if (ilinebits - olinebits) is a value in the range 1..7
            */
            uint diff = ilinebits - olinebits;
            /*input and output bit pointers*/
            int ibp = 0;
            int obp = 0;
            for (int i = 0; i < height; ++i)
            {
                for (int x = 0; x < olinebits; ++x)
                {
                    byte bit = BitHelper.ReadBitFromReversedStream(ref ibp, inputData);
                    BitHelper.SetBitOfReversedStream(ref obp, rawBuffer, bit);
                }

                ibp += (int)diff;
            }
        }

        

        /*in an idat chunk, each scanline is a multiple of 8 bits, unlike the lodepng output buffer,
        and in addition has one extra byte per line: the filter byte. So this gives a larger
        result than lodepng_get_raw_size. */
        private long GetRawSizeIdat(int width, int height, PNGColorMode colorMode)
        {
            var bpp = PNGColorConvertion.GetBitsPerPixel(colorMode);
            /* + 1 for the filter byte, and possibly plus padding bits per line */
            var line = ((width / 8) * bpp) + 1 + ((width & 7) * bpp + 7) / 8;
            return height * line;
        }

        internal static long GetRawSizeLct(int width, int height, PNGColorMode colorMode)
        {
            var bpp = PNGColorConvertion.GetBitsPerPixel(colorMode);
            var n = width * height;
            return ((n / 8) * bpp) + ((n & 7) * bpp + 7) / 8;
        }

        private void ReadtEXtChunk(PNGState state, uint chunkLength)
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
        private void ReadHeaderChunk(PNGState state, out int width, out int height)
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

            width = header.Width;
            height = header.Height;
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
        }

        private void ReadiCCPChunk(PNGState state, uint chunkSize)
        {
            var iccp = stream.ReadiCCP(state, this, chunkSize);
        }

        private void ReadsRGBChunk(PNGInfo info)
        {
            var srgb = stream.ReadsRGB();
            info.IsSrgbDefined = true;
            info.SrgbIntent = srgb.RenderingIntent;
        }

        private void ReadgAMAChunk(PNGInfo info)
        {
            var gama = stream.ReadgAMA();
            info.IsGamaDefined = true;
            info.Gamma = gama.Gamma;
        }

        private void ReadpHYsChunk(PNGInfo info)
        {
            var phys = stream.ReadpHYs();
            info.PhysX = phys.PhysX;
            info.PhysY = phys.PhysY;
            info.PhysUnit = phys.Unit;
            info.IsPhysDefined = true;
        }

        private uint CheckColorValidity(PNGColorType colorType, uint bitDepth)
        {
            switch(colorType)
            {
                case PNGColorType.Grey:
                    if (!(bitDepth == 1 || bitDepth == 2 || bitDepth == 4 || bitDepth == 8|| bitDepth == 16))
                    {
                        return 37;
                    }
                    break;
                case PNGColorType.GreyAlpha:
                case PNGColorType.RGB:
                case PNGColorType.RGBA:
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
        private bool CheckPixelOverflow(int width, int height, PNGColorMode pngColor, PNGColorMode rawColor)
        {
            ulong bpp = Math.Max(PNGColorConvertion.GetBitsPerPixel(pngColor), PNGColorConvertion.GetBitsPerPixel(rawColor));
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
