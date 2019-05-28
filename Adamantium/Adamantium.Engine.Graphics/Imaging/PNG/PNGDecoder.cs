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
            DecodeGeneric(state, out var colors, out int width, out int height);
            ImageDescription descr = new ImageDescription();
            descr.Width = width;
            descr.Height = height;
            descr.ArraySize = 1;
            descr.Format = AdamantiumVulkan.Core.Format.R8G8B8A8_UNORM;
            descr.MipLevels = 1;
            descr.Depth = 1;
            var img = Image.New(descr);
            var handle = GCHandle.Alloc(colors, GCHandleType.Pinned);
            Utilities.CopyMemory(img.DataPointer, handle.AddrOfPinnedObject(), img.PixelBuffer[0].BufferStride);
            handle.Free();

            return img;
        }

        private void DecodeGeneric(PNGState state, out byte[] colors, out int width, out int height)
        {
            bool IEND = false;
            string chunk;
            ulong i;
            /*the data from idat chunks*/
            List<byte> idat = new List<byte>();
            List<byte> scanlines = new List<byte>();
            ulong predict = 0;

            /*for unknown chunk order*/
            uint unknown = 0;
            /*1 = after IHDR, 2 = after PLTE, 3 = after IDAT*/
            uint criticalPos = 1;

            // initialize out parameters in case of errors
            colors = new byte[0];
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
                uint chunkLength = 0;

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
                chunkLength = (uint)stream.ReadChunkSize();

                if (chunkLength > int.MaxValue)
                {
                    if (state.DecoderSettings.IgnoreEnd)
                    {
                        /*other errors may still happen though*/
                        break;
                    }
                    state.Error = 63;
                }

                if ((stream.Position + chunkLength + 12) > stream.Length)
                {
                    /*error: size of the in buffer too small to contain next chunk*/
                    state.Error = 64;
                }

                var chunkType = stream.ReadChunkType();

                switch (chunkType)
                {
                    case "IDAT":
                        var bytes = stream.ReadBytes((int)chunkLength);
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
                            ReadtEXtChunk(state, chunkLength);
                        }
                        break;
                    case "zTXt":
                        break;
                    case "iTXt":
                        break;
                    case "tIME":
                        break;
                    case "pHYs":
                        ReadpHYsChunk(state.InfoPng);
                        break;
                    case "gAMA":
                        ReadgAMAChunk(state.InfoPng);
                        break;
                    case "cHRM":
                        break;
                    case "sRGB":
                        ReadsRGBChunk(state.InfoPng);
                        break;
                    case "iCCP":
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
                    currentPosition += chunkLength + 12;
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
            //colors = new byte[width * height * 4];
            colors = new byte[predict];
            Decompress(idat, state.DecoderSettings, colors);
        }

        private uint Decompress(List<byte> inputData, PNGDecoderSettings settings, byte[] outData)
        {
            uint error = 0;
            int CM, CINFO, FDICT;

            if (inputData.Count < 2)
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
            var range = inputData.ToArray()[2..];
            error = Inflate(range, outData);

            return error;
        }

        private uint Inflate(byte[] inputData, byte[] outData)
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
                //cnt++;
                //Debug.WriteLine(cnt);
                if (error > 0)
                {
                    return error;
                }
            }

            return error;
        }

        private int cnt = 0;

        private uint InflateNoCompression(byte[] inputData, ref int bitPointer, ref int pos, ref byte[] outData)
        {
            int position;
            uint LEN, NLEN, n, error = 0;
            outData = new byte[0];

            /*go to first boundary of byte*/
            while (((bitPointer) & 0x07) != 0) ++bitPointer;
            
            /*byte position*/
            position = bitPointer / 8;

            /*read LEN (2 bytes) and NLEN (2 bytes)*/
            if (position +4 >= inputData.Length)
            {
                /*error, bit pointer will jump past memory*/
                return 52;
            }

            LEN = inputData[position] + 256u + inputData[position + 1];
            position += 2;
            NLEN = inputData[position] + 256u * inputData[position + 1];
            position += 2;

            /*check if 16-bit NLEN is really the one's complement of LEN*/
            if (LEN + NLEN != ushort.MaxValue)
            {
                /*error: NLEN is not one's complement of LEN*/
                return 21;
            }

            //Array.Resize<byte>(ref outData, (int)(pos + LEN));

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
        private uint InflateHuffmanBlock(byte[] inputData, ref int bitPointer, ref int pos, uint bType, ref byte[] outData)
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

            cnt = 0;
            Stopwatch TIMER;
            TIMER = Stopwatch.StartNew();
            while (error == 0) /*decode all symbols until end reached, breaks at end code*/
            {
                /*code_ll is literal, length or end code*/
                int codeLL = HuffmanTree.DecodeSymbol(inputData, ref bitPointer, treeLL, bitLength);

                if (codeLL <= 255) /*literal symbol*/
                {
                    //Array.Resize(ref outData, pos + 1);
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

                    //Array.Resize(ref outData, (int)(pos + length));
                    if (distance < length)
                    {
                        for (forward = 0; forward < length; ++forward)
                        {
                            outData[pos++] = outData[backward++];
                        }
                    }
                    else
                    {
                        Buffer.BlockCopy(outData, pos, outData, (int)backward, (int)length);
                        pos += (int)length;
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
                
                cnt++;
            };
            TIMER.Stop();
            Debug.WriteLine($"{TIMER.ElapsedMilliseconds} - {cnt}");
            //Debug.WriteLine(cnt);
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
                bitlenCl = new uint[HuffmanTree.NumCodeLengthCodes * Marshal.SizeOf<uint>()];

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
                bitlenLL = new uint[HuffmanTree.NumDeflateCodeSymbols * Marshal.SizeOf<uint>()];
                bitlenDist = new uint[HuffmanTree.NumDistanceSymbols * Marshal.SizeOf<uint>()];

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

        /*in an idat chunk, each scanline is a multiple of 8 bits, unlike the lodepng output buffer,
        and in addition has one extra byte per line: the filter byte. So this gives a larger
        result than lodepng_get_raw_size. */
        private ulong GetRawSizeIdat(int width, int height, PNGColorMode colorMode)
        {
            var bpp = GetBitsPerPixel(colorMode);
            /* + 1 for the filter byte, and possibly plus padding bits per line */
            ulong line = ((ulong)(width / 8) * bpp) + 1 + ((ulong)(width & 7) * bpp + 7) / 8;
            return (ulong)height * line;
        }

        private void ReadtEXtChunk(PNGState state, uint chunkLength)
        {
            var text = stream.ReadtEXtChunk(state, chunkLength);
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

        private uint GetBitsPerPixel(PNGColorMode colorMode)
        {
            return (uint)GetNumberOfColorChannels(colorMode.ColorType) * colorMode.BitDepth;
        }

        private int GetNumberOfColorChannels(PNGColorType colorType)
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
