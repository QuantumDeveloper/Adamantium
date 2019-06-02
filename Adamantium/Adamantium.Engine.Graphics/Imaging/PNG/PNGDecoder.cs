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
        private PNGStream stream;
        public PNGDecoder(PNGStream stream)
        {
            this.stream = stream;
        }

        public Image Decode()
        {
            return Decode(PNGColorType.RGBA, 8);
        }

        private bool ReadPNGHeader()
        {
            var bytes = stream.ReadBytes(8);
            for(int i = 0; i<bytes.Length; ++i)
            {
                if (bytes[i] != PNGHelper.PngHeader[i])
                {
                    return false;
                }
            }
            return true;
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

            var bitsPerPixel = GetBitsPerPixel(state.InfoRaw);
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

            /*for unknown chunk order*/
            uint unknown = 0;
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
                chunkSize = (uint)stream.ReadChunkSize();

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
                        var bytes = stream.ReadBytes((int)chunkSize);
                        idat.AddRange(bytes);
                        break;
                    case "IEND":
                        IEND = true;
                        break;
                    case "PLTE":
                        break;
                    case "tRNS":
                        break;
                    case "bkGD":
                        break;
                    case "tEXt":
                        if (state.DecoderSettings.ReadTextChunks)
                        {
                            ReadtEXtChunk(state, chunkSize);
                        }
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

                        unknown = 1;

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
                predict += GetRawSizeIdat((width + 7) >> 3 , (height + 7) >> 3, state.InfoPng.ColorMode);
                if (width > 4)
                {
                    predict += GetRawSizeIdat((width + 3) >> 3, (height + 7) >> 3, state.InfoPng.ColorMode);
                }
                predict += GetRawSizeIdat((width + 3) >> 2, (height + 3) >> 3, state.InfoPng.ColorMode);
                if (width > 2)
                {
                    predict += GetRawSizeIdat((width + 1) >> 2, (height + 3) >> 2, state.InfoPng.ColorMode);
                }
                predict += GetRawSizeIdat((width + 1) >> 1, (height + 1) >> 2, state.InfoPng.ColorMode);
                if (width > 1)
                {
                    predict += GetRawSizeIdat((width) >> 1, (height + 1) >> 1, state.InfoPng.ColorMode);
                }
                predict += GetRawSizeIdat((width), (height) >> 1, state.InfoPng.ColorMode);
            }
            var scanlines = new List<byte>((int)predict);
            var error = Decompress(idat.ToArray(), state.DecoderSettings, scanlines);

            long bufferSize = 0;

            bufferSize = GetRawSizeLct(width, height, state.InfoPng.ColorMode);
            outBuffer = new byte[bufferSize];

            if (error == 0)
            {
                state.Error = PostProcessScanline(outBuffer, scanlines.ToArray(), width, height, state.InfoPng);
            }

            return error;
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

        internal uint Decompress(byte[] inputData, PNGDecoderSettings settings, List<byte> outData)
        {
            uint error = 0;
            int CM, CINFO, FDICT;

            if (inputData.Length < 2)
            {
                /*error, size of zlib data too small*/
                return 53;
            }

            /*read information from zlib header*/
            if ((inputData[0] * 256 + inputData[1]) % 31 != 0)
            {
                /*error: 256 * in[0] + in[1] must be a multiple of 31, the FCHECK value is supposed to be made that way*/
                return 24;
            }

            CM = inputData[0] & 15;
            CINFO = (inputData[0] >> 4) & 15;
            /*FCHECK = in[1] & 31;*/ /*FCHECK is already tested above*/
            FDICT = (inputData[1] >> 5) & 1;
            /*FLEVEL = (in[1] >> 6) & 3;*/ /*FLEVEL is not used here*/

            if (CM != 8 || CINFO > 7)
            {
                /*error: only compression method 8: inflate with sliding window of 32k is supported by the PNG spec*/
                return 25;
            }
            if (FDICT != 0)
            {
                /*error: the specification of PNG says about the zlib stream:
                "The additional flags shall not specify a preset dictionary."*/
                return 26;
            }
            var range = inputData[2..];
            error = Inflate(range, outData);

            if (!settings.IgnoreAdler32)
            {
                var ADLER32 = ReadInt32FromArray(inputData, inputData.Length - 4);
                var checksum = Adler32(outData.ToArray());

                if (checksum != ADLER32)
                {
                    /*error, adler checksum not correct, data must be corrupted*/
                    return 58;
                }
            }

            return error;
        }

        private uint Adler32(byte[] data)
        {
            return UpdateAdler32(1, data);
        }

        private uint UpdateAdler32(uint adler, byte[] data)
        {
            uint s1 = adler & 0xffff;
            uint s2 = (adler >> 16) & 0xffff;
            var len = data.Length;
            int i = 0;

            while (len > 0)
            {
                /*at least 5552 sums can be done before the sums overflow, saving a lot of module divisions*/
                var amount = len > 5552 ? 5552 : len;
                len -= amount;
                while (amount > 0)
                {
                    s1 += data[i++];
                    s2 += s1;
                    --amount;
                }
                s1 %= 65521;
                s2 %= 65521;
            }

            return (s2 << 16) | s1;
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
            var bpp = GetBitsPerPixel(infoPng.ColorMode);
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
                            error = Unfilter(inPtr, inPtr, width, height, (int)bpp);
                            if (error > 0)
                            {
                                return error;
                            }
                            RemovePaddingBits(rawPtr, inPtr, (uint)(width * bpp), (uint)((width * bpp + 7) / 8) * 8, (uint)height);
                        }
                        else
                        {
                            error = Unfilter(rawPtr, inPtr, width, height, (int)bpp);
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
                            error = Unfilter(rawPtr, inPtr, (int)passWidth[i], (int)passHeight[i], (int)bpp);
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
                    byte bit = ReadBitFromReversedStream(ref ibp, inputData);
                    SetBitOfReversedStream(ref obp, rawBuffer, bit);
                }

                ibp += (int)diff;
            }
        }

        private unsafe uint Unfilter(byte* rawBuffer, byte* inputData, int width, int height, int bpp)
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

        private unsafe uint UnfilterScanline(byte* recon, byte* scanline, byte* precon, int bytewidth, byte filterType, int length)
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

        /*
        Paeth predictor, used by PNG filter type 4
        The parameters are of type short, but should come from unsigned chars, the shorts
        are only needed to make the paeth calculation correct.
        */
        static byte PaethPredictor(short a, short b, short c)
        {
            short pa = (short)Math.Abs(b - c);
            short pb = (short)Math.Abs(a - c);
            short pc = (short)Math.Abs(a + b - c - c);

            if (pc < pa && pc < pb) return (byte)c;
            else if (pb < pa) return (byte)b;
            else return (byte)a;
        }

        private uint ReadInt32FromArray(byte[] buffer, int startIndex)
        {
            return (uint)((buffer[startIndex] << 24) | (buffer[startIndex + 1] << 16) | (buffer[startIndex + 2] << 8) | buffer[startIndex + 3]);
        }

        private uint Inflate(byte[] inputData, List<byte> outData)
        {
            /*bit pointer in the "in" data, current byte is bp >> 3, current bit is bp & 0x7 (from lsb to msb of the byte)*/
            int bp = 0;
            byte BFINAL = 0;
            int pos = 0; /*byte position in the out buffer*/
            uint error = 0;

            while(BFINAL == 0)
            {
                uint BTYPE;
                if (bp +2 >= inputData.Length * 8)
                {
                    return 52;
                }
                BFINAL = ReadBitFromStream(ref bp, inputData);
                BTYPE = 1u * ReadBitFromStream(ref bp, inputData);
                BTYPE += 2u * ReadBitFromStream(ref bp, inputData);

                if (BTYPE == 3)
                {
                    /*error: invalid BTYPE*/
                    return 20;
                }
                else if (BTYPE == 0)
                {
                    error = InflateNoCompression(inputData, ref bp, ref pos, ref outData);
                }
                else
                {
                    error = InflateHuffmanBlock(inputData, ref bp, ref pos, BTYPE, ref outData);
                }

                if (error > 0)
                {
                    return error;
                }
            }

            if (pos != outData.Count)
            {
                error = 91;
            }

            return error;
        }

        private uint InflateNoCompression(byte[] inputData, ref int bitPointer, ref int pos, ref List<byte> outData)
        {
            int position;
            uint LEN, NLEN, n, error = 0;

            /*go to first boundary of byte*/
            while (((bitPointer) & 0x7) != 0) ++bitPointer;
            
            /*byte position*/
            position = bitPointer / 8;

            /*read LEN (2 bytes) and NLEN (2 bytes)*/
            if (position +4 >= inputData.Length)
            {
                /*error, bit pointer will jump past memory*/
                return 52;
            }

            LEN = inputData[position] + 256u * inputData[position + 1];
            position += 2;
            NLEN = inputData[position] + 256u * inputData[position + 1];
            position += 2;

            /*check if 16-bit NLEN is really the one's complement of LEN*/
            if (LEN + NLEN != ushort.MaxValue)
            {
                /*error: NLEN is not one's complement of LEN*/
                return 21;
            }

            outData.AddRange(new byte[LEN]);

            /*read the literal data: LEN bytes are now stored in the out buffer*/
            if (position + LEN > inputData.Length)
            {
                return 21;
            }

            for (n = 0; n < LEN; ++n)
            {
                outData[pos++] = inputData[position++];
            }

            bitPointer = position * 8;

            return error;
        }

        /*inflate a block with dynamic of fixed Huffman tree*/
        private uint InflateHuffmanBlock(byte[] inputData, ref int bitPointer, ref int pos, uint bType, ref List<byte> outData)
        {
            uint error = 0;
            HuffmanTree treeLL; /*the huffman tree for literal and length codes*/
            HuffmanTree treeDist; /*the huffman tree for distance codes*/
            int bitLength = inputData.Length * 8;

            treeLL = new HuffmanTree();
            treeDist = new HuffmanTree();

            if (bType == 1)
            {
                GetTreeInflateFixed(treeLL, treeDist);
            }
            else if (bType == 2)
            {
                error = GetHuffmanTreeInflateDynamic(treeLL, treeDist, inputData, ref bitPointer);
            }

            Stopwatch TIMER;
            TIMER = Stopwatch.StartNew();
            while (error == 0) /*decode all symbols until end reached, breaks at end code*/
            {
                /*code_ll is literal, length or end code*/
                int codeLL = HuffmanTree.DecodeSymbol(inputData, ref bitPointer, treeLL, bitLength);

                if (codeLL <= 255) /*literal symbol*/
                {
                    outData.Add(0);
                    outData[pos] = (byte)codeLL;
                    ++pos;
                }
                /*length code*/
                else if (codeLL >= HuffmanTree.FirstLengthCodeIndex && codeLL <= HuffmanTree.LastLengthCodeIndex)
                {
                    int codeDist;
                    uint distance;
                    uint numExtrabitsL, numExtrabitsD; /*extra bits for length and distance*/
                    uint start, forward, backward, length;

                    /*part 1: get length base*/
                    length = HuffmanTree.LengthBase[codeLL - HuffmanTree.FirstLengthCodeIndex];

                    /*part 2: get extra bits and add the value of that to length*/
                    numExtrabitsL = HuffmanTree.LengthExtra[codeLL - HuffmanTree.FirstLengthCodeIndex];
                    if (bitPointer + numExtrabitsL > bitLength)
                    {
                        /*error, bit pointer will jump past memory*/
                        return 51;
                    }

                    length += ReadBitsFromStream(ref bitPointer, inputData, (int)numExtrabitsL);

                    /*part 3: get distance code*/
                    codeDist = HuffmanTree.DecodeSymbol(inputData, ref bitPointer, treeDist, bitLength);
                    if (codeDist > 29)
                    {
                        if (codeDist == -1)
                        {
                            /*huffmanDecodeSymbol returns (unsigned)(-1) in case of error*/
                            /*return error code 10 or 11 depending on the situation that happened in huffmanDecodeSymbol
                            (10=no endcode, 11=wrong jump outside of tree)*/
                            error = bitPointer > length * 8 ? 10u : 11u;
                        }
                        else
                        {
                            /*error: invalid distance code (30-31 are never used)*/
                            error = 18;
                        }
                        break;
                    }
                    distance = HuffmanTree.DistanceBase[codeDist];

                    /*part 4: get extra bits from distance*/
                    numExtrabitsD = HuffmanTree.DistanceExtra[codeDist];
                    if (bitPointer + numExtrabitsD > bitLength)
                    {
                        /*error, bit pointer will jump past memory*/
                        return 51;
                    }
                    distance += ReadBitsFromStream(ref bitPointer, inputData, (int)numExtrabitsD);

                    /*part 5: fill in all the out[n] values based on the length and dist*/
                    start = (uint)pos;
                    if (distance > start)
                    {
                        /*too long backward distance*/
                        return 52;
                    }
                    backward = start - distance;

                    //Add new elements to list
                    for (int i = 0; i < length; ++i)
                    {
                        outData.Add(0);
                    }

                    if (distance < length)
                    {
                        for (forward = 0; forward < length; ++forward)
                        {
                            outData[pos++] = outData[(int)backward++];
                        }
                    }
                    else
                    {
                        for (int i = 0; i< length; ++i)
                        {
                            outData[pos++] = outData[(int)backward++];
                        }
                    }
                }
                else if (codeLL == 256)
                {
                    break; /*end code, break the loop*/
                }
                else
                {
                    /*if(code == (unsigned)(-1))*/ /*huffmanDecodeSymbol returns (unsigned)(-1) in case of error*/
                    /*return error code 10 or 11 depending on the situation that happened in huffmanDecodeSymbol
                    (10=no endcode, 11=wrong jump outside of tree)*/
                    error = bitPointer > inputData.Length * 8 ? 10u : 11u;
                    break;
                }
            };

            TIMER.Stop();
            return error;
        }

        /*Get the tree of a deflated block with fixed tree, as specified in the deflate specification*/
        private void GetTreeInflateFixed(HuffmanTree treeLL, HuffmanTree treeDist)
        {
            HuffmanTree.GenerateFixedLitLenTree(treeLL);
            HuffmanTree.GenerateFixedDistanceTree(treeDist);
        }

        private uint GetHuffmanTreeInflateDynamic(HuffmanTree treeLL, HuffmanTree treeDist, byte[] inputData, ref int bitPointer)
        {
            uint error = 0;
            uint n, HLIT, HDIST, HCLEN, i;
            int bitLength = inputData.Length * 8;

            /*see comments in deflateDynamic for explanation of the context and these variables, it is analogous*/
            uint[] bitlenLL = null; /*lit,len code lengths*/
            uint[] bitlenDist = null; /*dist code lengths*/

            /*code length code lengths ("clcl"), the bit lengths of the huffman tree used to compress bitlen_ll and bitlen_d*/
            uint[] bitlenCl = null;
            HuffmanTree treeCl = null;

            if (bitPointer + 14 > (inputData.Length << 3))
            {
                /*error: the bit pointer is or will go past the memory*/
                return 49;
            }

            /*number of literal/length codes + 257. Unlike the spec, the value 257 is added to it here already*/
            HLIT = ReadBitsFromStream(ref bitPointer, inputData, 5) + 257;
            /*number of distance codes. Unlike the spec, the value 1 is added to it here already*/
            HDIST = ReadBitsFromStream(ref bitPointer, inputData, 5) + 1;
            /*number of code length codes. Unlike the spec, the value 4 is added to it here already*/
            HCLEN = ReadBitsFromStream(ref bitPointer, inputData, 4) + 4;

            if (bitPointer + HCLEN * 3 > inputData.Length << 3)
            {
                /*error: the bit pointer is or will go past the memory*/
                return 50;
            }

            treeCl = new HuffmanTree();

            while(error == 0)
            {
                /*read the code length codes out of 3 * (amount of code length codes) bits*/
                bitlenCl = new uint[HuffmanTree.NumCodeLengthCodes];

                for (i = 0; i != HuffmanTree.NumCodeLengthCodes; ++i)
                {
                    if (i < HCLEN)
                    {
                        bitlenCl[HuffmanTree.ClclOrder[i]] = ReadBitsFromStream(ref bitPointer, inputData, 3);
                    }
                    else
                    {
                        /*if not, it must stay 0*/
                        bitlenCl[HuffmanTree.ClclOrder[i]] = 0;
                    }
                }

                error = HuffmanTree.MakeFromLength(treeCl, bitlenCl, HuffmanTree.NumCodeLengthCodes, 7);

                if (error != 0) break;

                /*now we can use this tree to read the lengths for the tree that this function will return*/
                bitlenLL = new uint[HuffmanTree.NumDeflateCodeSymbols];
                bitlenDist = new uint[HuffmanTree.NumDistanceSymbols];

                i = 0;
                while( i < HLIT + HDIST)
                {
                    int code = HuffmanTree.DecodeSymbol(inputData, ref bitPointer, treeCl, bitLength);
                    if (code <= 15) /*a length code*/
                    {
                        if (i < HLIT)
                        {
                            bitlenLL[i] = (uint)code;
                        }
                        else
                        {
                            bitlenDist[i - HLIT] = (uint)code;
                        }
                        ++i;
                    }
                    else if (code == 16) /*repeat previous*/
                    {
                        /*read in the 2 bits that indicate repeat length (3-6)*/
                        uint repLength = 3;
                        /*set value to the previous code*/
                        uint value;

                        if (i == 0) return 54; /*can't repeat previous if i is 0*/

                        /*error, bit pointer jumps past memory*/
                        if (bitPointer + 2 > bitLength) return 50;

                        repLength += ReadBitsFromStream(ref bitPointer, inputData, 2);

                        if (i < HLIT + 1)
                        {
                            value = bitlenLL[i - 1];
                        }
                        else
                        {
                            value = bitlenDist[i - HLIT - 1];
                        }

                        /*repeat this value in the next lengths*/
                        for (n = 0; n< repLength; ++n)
                        {
                            if (i >= HLIT + HDIST)
                            {
                                /*error: i is larger than the amount of codes*/
                                return 13;
                            }

                            if (i < HLIT)
                            {
                                bitlenLL[i] = value;
                            }
                            else
                            {
                                bitlenDist[i - HLIT] = value;
                            }
                            ++i;
                        }
                    }
                    else if (code == 17) /*repeat "0" 3-10 times*/
                    {
                        /*read in the bits that indicate repeat length*/
                        uint repLength = 3;

                        if (bitPointer + 3 > bitLength) return 50; /*error, bit pointer jumps past memory*/

                        repLength += ReadBitsFromStream(ref bitPointer, inputData, 3);

                        for (n = 0; n< repLength; ++n)
                        {
                            if (i >= HLIT + HDIST)
                            {
                                /*error: i is larger than the amount of codes*/
                                return 14;
                            }

                            if (i < HLIT)
                            {
                                bitlenLL[i] = 0;
                            }
                            else
                            {
                                bitlenDist[i - HLIT] = 0;
                            }
                            ++i;
                        }
                    }
                    else if (code == 18) /*repeat "0" 11-138 times*/
                    {
                        /*read in the bits that indicate repeat length*/
                        uint repLength = 11;

                        if (bitPointer + 7 > bitLength)
                        {
                            /*error, bit pointer jumps past memory*/
                            return 50;
                        }

                        repLength += ReadBitsFromStream(ref bitPointer, inputData, 7);

                        /*repeat this value in the next lengths*/
                        for (n = 0; n< repLength; ++n)
                        {
                            if (i >= HLIT + HDIST)
                            {
                                /*error: i is larger than the amount of codes*/
                                return 15;
                            }

                            if (i < HLIT)
                            {
                                bitlenLL[i] = 0;
                            }
                            else
                            {
                                bitlenDist[i - HLIT] = 0;
                            }
                            ++i;
                        }
                    }
                    else /*if(code == (unsigned)(-1))*/ /*huffmanDecodeSymbol returns (unsigned)(-1) in case of error*/
                    {
                        if (code == -1)
                        {
                            /*return error code 10 or 11 depending on the situation that happened in huffmanDecodeSymbol
                            (10=no endcode, 11=wrong jump outside of tree)*/

                            error = bitPointer > bitLength ? 10u : 11u;
                        }
                        else
                        {
                            /*unexisting code, this can never happen*/
                            error = 16;
                        }
                        break;
                    }
                };

                if (error > 0) break;

                if (bitlenLL[256] == 0) return 64; /*the length of the end code 256 must be larger than 0*/

                /*now we've finally got HLIT and HDIST, so generate the code trees, and the function is done*/
                error = HuffmanTree.MakeFromLength(treeLL, bitlenLL, HuffmanTree.NumDeflateCodeSymbols, 15);
                if (error > 0) break;
                error = HuffmanTree.MakeFromLength(treeDist, bitlenDist, HuffmanTree.NumDistanceSymbols, 15);

                break;
            };

            return error;
        }
        private byte ReadBitFromStream(ref int bitPointer, byte[] data)
        {
            byte result = (byte)((data[bitPointer >> 3] >> (bitPointer & 0x7)) & 1);
            ++bitPointer;
            return result;
        }

        private uint ReadBitsFromStream(ref int bitPointer, byte[] data, int count)
        {
            uint result = 0;
            for (int i = 0; i!= count; ++i)
            {
                result += (uint)((data[bitPointer >> 3] >> (bitPointer & 0x7)) & 1) << i;
                ++bitPointer;
            }
            return result;
        }

        internal static unsafe byte ReadBitFromReversedStream(ref int bitPointer, byte* data)
        {
            byte result = (byte)((data[bitPointer >> 3] >> (7 - (bitPointer & 0x7))) & 1);
            ++bitPointer;
            return result;
        }

        internal static unsafe uint ReadBitsFromReversedStream(ref int bitPointer, byte* data, int count)
        {
            uint result = 0;
            for (int i = 0; i < count; ++i)
            {
                result <<= 1;
                result |= ReadBitFromReversedStream(ref bitPointer, data);
                ++bitPointer;
            }
            return result;
        }

        private unsafe void SetBitOfReversedStream(ref int bitPointer, byte* bitStream, byte bit)
        {
            /*the current bit in bitstream may be 0 or 1 for this to work*/
            if (bit == 0)
            {
                bitStream[bitPointer >> 3] &= (byte)~(1 << (7 - (bitPointer & 0x7)));
            }
            else
            {
                bitStream[bitPointer >> 3] |= (byte)(1 << (7 - (bitPointer & 0x7)));
            }
            ++bitPointer;
        }

        internal static unsafe void SetBitOfReversedStream0(ref int bitPointer, byte* bitStream, byte bit)
        {
            /*the current bit in bitstream must be 0 for this to work*/
            if (bit > 0)
            {
                /*earlier bit of huffman code is in a lesser significant bit of an earlier byte*/
                bitStream[bitPointer >> 3] |= (byte)(bit << (7 - (bitPointer & 0x7)));
            }
            ++bitPointer;
        }

        /*in an idat chunk, each scanline is a multiple of 8 bits, unlike the lodepng output buffer,
        and in addition has one extra byte per line: the filter byte. So this gives a larger
        result than lodepng_get_raw_size. */
        private long GetRawSizeIdat(int width, int height, PNGColorMode colorMode)
        {
            var bpp = GetBitsPerPixel(colorMode);
            /* + 1 for the filter byte, and possibly plus padding bits per line */
            var line = ((width / 8) * bpp) + 1 + ((width & 7) * bpp + 7) / 8;
            return height * line;
        }

        internal static long GetRawSizeLct(int width, int height, PNGColorMode colorMode)
        {
            var bpp = GetBitsPerPixel(colorMode);
            var n = width * height;
            return ((n / 8) * bpp) + ((n & 7) * bpp + 7) / 8;
        }

        private void ReadtEXtChunk(PNGState state, uint chunkLength)
        {
            var text = stream.ReadtEXt(state, chunkLength);
            state.InfoPng.TextKeys.Add(text.Key);
            state.InfoPng.TextStrings.Add(text.Text);
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

            if (!ReadPNGHeader())
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
            ulong bpp = Math.Max(GetBitsPerPixel(pngColor), GetBitsPerPixel(rawColor));
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

        private static uint GetBitsPerPixel(PNGColorMode colorMode)
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
