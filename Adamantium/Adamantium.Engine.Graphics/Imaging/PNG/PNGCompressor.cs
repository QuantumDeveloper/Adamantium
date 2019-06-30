using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public class PNGCompressor
    {
        #region Compressor

        private unsafe uint EncodeLZ77(byte[] inData, int inPos, int inSize, List<int> outData, Hash hash,
            int windowSize, int minmatch, int nicematch, bool lazymatching)
        {
            uint error = 0;

            int pos = 0;
            /*for large window lengths, assume the user wants no compression loss. Otherwise, max hash chain length speedup.*/
            int maxChainLength = windowSize >= 8192 ? windowSize : windowSize / 8;
            int maxLazyMatch = windowSize >= 8192 ? Hash.MaxSupportedDeflateLength : 64;

            /*not sure if setting it to false for windowsize < 8192 is better or worse*/
            bool useZeros = true;
            uint numZeros = 0;

            int offset; /*the offset represents the distance in LZ77 terminology*/
            int length;
            bool lazy = false;
            int lazylength = 0, lazyoffset = 0;
            int hashval;
            int currentOffset, currentLength;
            int prevOffset;
            int hashpos;

            if (windowSize == 0 || windowSize > short.MaxValue+1)
            {
                /*error: windowsize smaller/larger than allowed*/
                return 60;
            }

            if ((windowSize & (windowSize - 1)) != 0)
            {
                /*error: must be power of two*/
                return 90;
            }

            if (nicematch > Hash.MaxSupportedDeflateLength)
            {
                nicematch = Hash.MaxSupportedDeflateLength;
            }

            for (pos = inPos; pos < inSize; ++pos)
            {
                /*position for in 'circular' hash buffers*/
                int wpos = pos & (windowSize - 1);
                int chainLength = 0;

                hashval = hash.GetHash(inData, inSize, pos);

                if (useZeros && hashval == 0)
                {
                    if (numZeros == 0)
                    {
                        numZeros = hash.CountZeros(inData, inSize, pos);
                    }
                    else if (pos + numZeros > inSize || inData[pos + numZeros - 1] != 0)
                    {
                        --numZeros;
                    }
                }
                else
                {
                    numZeros = 0;
                }

                hash.UpdateHashChain(wpos, hashval, (ushort)numZeros);

                /*the length and offset found for the current position*/
                length = 0;
                offset = 0;

                if (pos == 672094)
                {

                }

                hashpos = hash.Chain[wpos];
                int index = inSize < pos + Hash.MaxSupportedDeflateLength ? inSize : pos + Hash.MaxSupportedDeflateLength;
                fixed (byte* inDataPtr = &inData[0])
                {
                    byte* lastPtr = inDataPtr + index;
                    prevOffset = 0;
                    for (; ; )
                    {
                        if (chainLength++ > maxChainLength)
                        {
                            break;
                        }

                        currentOffset = hashpos <= wpos ? wpos - hashpos : wpos - hashpos + windowSize;

                        if (currentOffset < prevOffset) break; /*stop when went completely around the circular buffer*/

                        prevOffset = currentOffset;
                        if (currentOffset > 0)
                        {
                            fixed (byte* forePtr = &inData[pos])
                            {
                                var forePtrCopy = forePtr;
                                fixed (byte* backPtr = &inData[pos - currentOffset])
                                {
                                    var backPtrCopy = backPtr;
                                    /*common case in PNGs is lots of zeros. Quickly skip over them as a speedup*/
                                    if (numZeros >= 3)
                                    {
                                        var skip = hash.Zeros[hashpos];
                                        if (skip > numZeros)
                                        {
                                            skip = (ushort)numZeros;
                                        }
                                        backPtrCopy += skip;
                                        forePtrCopy += skip;
                                    }

                                    /*maximum supported length by deflate is max length*/
                                    while (forePtrCopy != lastPtr && *backPtrCopy == *forePtrCopy)
                                    {
                                        ++backPtrCopy;
                                        ++forePtrCopy;
                                    };

                                    currentLength = (int)(forePtrCopy - forePtr);

                                    if (currentLength > length)
                                    {
                                        /*the longest length*/
                                        length = currentLength;
                                        /*the offset that is related to this longest length*/
                                        offset = currentOffset;
                                        /*jump out once a length of max length is found (speed gain). This also jumps
                                        out if length is MAX_SUPPORTED_DEFLATE_LENGTH*/
                                        if (currentLength >= nicematch) break;
                                    }
                                }
                            }
                        }

                        if (hashpos == hash.Chain[hashpos]) break;

                        if (numZeros >= 3 && length > numZeros)
                        {
                            hashpos = hash.Chainz[hashpos];
                            if (hash.Zeros[hashpos] != numZeros) break;
                        }
                        else
                        {
                            hashpos = hash.Chain[hashpos];
                            /*outdated hash value, happens if particular value was not encountered in whole last window*/
                            if (hash.Val[hashpos] != hashval) break;
                        }
                    }

                    if (lazymatching)
                    {
                        if (!lazy
                            && length >= 3
                            && length <= maxLazyMatch
                            && length < Hash.MaxSupportedDeflateLength)
                        {
                            lazy = true;
                            lazylength = length;
                            lazyoffset = offset;
                            continue; /*try the next byte*/
                        }

                        if (lazy)
                        {
                            lazy = false;
                            if (pos == 0) return 81;
                            if (length > lazylength + 1)
                            {
                                /*push the previous character as literal*/
                                outData.Add(inData[pos - 1]);
                            }
                            else
                            {
                                length = lazylength;
                                offset = lazyoffset;
                                hash.Head[hashval] = -1; /*the same hashchain update will be done, this ensures no wrong alteration*/
                                hash.Headz[numZeros] = -1; /*idem*/
                                --pos;
                            }
                        }
                    }

                    if (length >= 3 && offset > windowSize) return 86; /*too big (or overflown negative) offset*/

                    /*encode it as length/distance pair or literal value*/
                    if (length < 3) /*only lengths of 3 or higher are supported as length/distance pair*/
                    {
                        outData.Add(inData[pos]);
                    }
                    else if (length < minmatch || (length == 3 && offset > 4096))
                    {
                        /*compensate for the fact that longer offsets have more extra bits, a
                        length of only 3 may be not worth it then*/
                        outData.Add(inData[pos]);
                    }
                    else
                    {
                        AddLengthDistance(outData, length, offset);
                        for (int i = 1; i < length; ++i)
                        {
                            ++pos;
                            wpos = pos & (windowSize - 1);
                            hashval = hash.GetHash(inData, inSize, pos);
                            if (useZeros && hashval == 0)
                            {
                                if (numZeros == 0)
                                {
                                    numZeros = hash.CountZeros(inData, inSize, pos);
                                }
                                else if (pos + numZeros > inSize || inData[pos + numZeros - 1] != 0)
                                {
                                    --numZeros;
                                }
                            }
                            else
                            {
                                numZeros = 0;
                            }
                            hash.UpdateHashChain(wpos, hashval, (ushort)numZeros);
                        }
                    }
                }
            } /*end of the loop through each character of input*/

            return error;
        }

        private void WriteLZ77Data(ref int bitPointer, List<int> lz77Encoded, List<byte> outData, HuffmanTree treeLL, HuffmanTree treeDist)
        {
            for (int i = 0; i != lz77Encoded.Count; ++i)
            {
                var val = lz77Encoded[i];
                BitHelper.AddBitsToStreamReversed(ref bitPointer, outData, treeLL.GetCode(val), (int)treeLL.GetLength(val));
                if (val > 256) /*for a length code, 3 more things have to be added*/
                {
                    var lengthIndex = val - HuffmanTree.FirstLengthCodeIndex;
                    var nLengthExtraBits = (int)HuffmanTree.LengthExtra[lengthIndex];
                    var lengthExtraBits = (uint)lz77Encoded[++i];

                    var distanceCode = lz77Encoded[++i];

                    var distanceIndex = distanceCode;
                    var nDistanceExtraBits = HuffmanTree.DistanceExtra[distanceIndex];
                    var distanceExtraBits = lz77Encoded[++i];

                    BitHelper.AddBitsToStream(ref bitPointer, outData, lengthExtraBits, nLengthExtraBits);
                    BitHelper.AddBitsToStreamReversed(ref bitPointer, outData, treeDist.GetCode(distanceCode), (int)treeDist.GetLength(distanceCode));
                    BitHelper.AddBitsToStream(ref bitPointer, outData, (uint)distanceExtraBits, (int)nDistanceExtraBits);
                }
            }
        }

        private void AddLengthDistance(List<int> values, int length, int distance)
        {
            /*values in encoded vector are those used by deflate:
            0-255: literal bytes
            256: end
            257-285: length/distance pair (length code, followed by extra length bits, distance code, extra distance bits)
            286-287: invalid*/

            var lengthCode = SearchCodeIndex(HuffmanTree.LengthBase, length);
            var extraLength = length - HuffmanTree.LengthBase[lengthCode];
            var distCode = SearchCodeIndex(HuffmanTree.DistanceBase, distance);
            var extraDistance = distance - HuffmanTree.DistanceBase[distCode];

            values.Add(lengthCode + (int)HuffmanTree.FirstLengthCodeIndex);
            values.Add((int)extraLength);
            values.Add(distCode);
            values.Add((int)extraDistance);
        }

        private int SearchCodeIndex(uint[] array, int value)
        {
            int left = 1;
            int right = array.Length - 1;

            while (left <= right)
            {
                int mid = (left + right) >> 1;
                if (array[mid] >= value)
                {
                    right = mid - 1;
                }
                else
                {
                    left = mid + 1;
                }
            }
            if (left >= array.Length || array[left] > value) left--;

            return left;
        }

        public uint Compress(byte[] rawData, PNGEncoderSettings settings, List<byte> outData)
        {
            uint error = 0;

            /*zlib data: 1 byte CMF (CM+CINFO), 1 byte FLG, deflate data, 4 byte ADLER32 checksum of the Decompressed data*/
            uint CMF = 120; /*0b01111000: CM 8, CINFO 7. With CINFO 7, any window size up to 32768 can be used.*/
            uint FLEVEL = 0;
            uint FDICT = 0;
            uint CMFFLG = 256 * CMF + FDICT * 32 + FLEVEL * 64;
            uint FCHECK = 31 - CMFFLG % 31;
            CMFFLG += FCHECK;

            outData.Add((byte)(CMFFLG >> 8));
            outData.Add((byte)(CMFFLG & 255));

            var deflatedData = new List<byte>();
            error = Deflate(rawData, settings, deflatedData);

            if (error == 0)
            {
                uint ADLER32 = Adler32.GetAdler32(rawData);
                outData.AddRange(deflatedData);
                BitHelper.Add32BitInt(outData, ADLER32);
            }

            return error;
        }

        private uint Deflate(byte[] inData, PNGEncoderSettings settings, List<byte> outData)
        {
            uint error = 0;
            int blockSize = 0;
            int numDeflateBlocks = 0;
            Hash hash = null;
            int bitPointer = 0;

            if (settings.BType > 2)
            {
                return 61;
            }
            else if (settings.BType == 0)
            {
                return DeflateNoCompression(inData, outData);
            }
            else if (settings.BType == 1)
            {
                blockSize = inData.Length;
            }
            else /*if(settings.BType == 2)*/
            {
                /*on PNGs, deflate blocks of 65-262k seem to give most dense encoding*/
                blockSize = inData.Length / 8 + 8;
                if (blockSize < ushort.MaxValue + 1) blockSize = ushort.MaxValue + 1;
                if (blockSize > 262144) blockSize = 262144;
            }

            numDeflateBlocks = (inData.Length + blockSize - 1) / blockSize;
            if (numDeflateBlocks == 0) numDeflateBlocks = 1;

            hash = new Hash();
            hash.Initialize((int)settings.CompressionLevel);

            for (int i = 0; i != numDeflateBlocks && error == 0; ++i)
            {
                var final = Convert.ToByte(i == numDeflateBlocks - 1);
                var start = i * blockSize;
                var end = start + blockSize;
                if (end > inData.Length) end = inData.Length;

                if (settings.BType == 1)
                {
                    error = DeflateFixed(inData, outData, ref bitPointer, ref hash, start, end, settings, final);
                }
                else if (settings.BType == 2)
                {
                    error = DeflateDynamic(inData, outData, ref bitPointer, ref hash, start, end, settings, final);
                }
            }

            return error;
        }

        private uint DeflateFixed(byte[] data, List<byte> outData, ref int bitPointer, ref Hash hash, int dataPos, int dataEnd, PNGEncoderSettings settings, byte final)
        {
            uint error = 0;

            HuffmanTree treeLL = new HuffmanTree(); /*tree for literal values and length codes*/
            HuffmanTree treeDist = new HuffmanTree(); /*tree for distance codes*/
            HuffmanTree.GenerateFixedLitLenTree(treeLL);
            HuffmanTree.GenerateFixedDistanceTree(treeDist);

            byte BFINAL = final;

            BitHelper.AddBitToStream(ref bitPointer, outData, BFINAL);
            BitHelper.AddBitToStream(ref bitPointer, outData, 1); /*first bit of BTYPE*/
            BitHelper.AddBitToStream(ref bitPointer, outData, 0); /*second bit of BTYPE*/

            if (settings.UseLZ77)
            {
                List<int> lz77Encoded = new List<int>();
                error = EncodeLZ77(data, dataPos, dataEnd, lz77Encoded, hash, (int)settings.CompressionLevel, (int)settings.MinMatch, (int)settings.NiceMatch, settings.LazyMatching);
                if (error == 0)
                {
                    WriteLZ77Data(ref bitPointer, lz77Encoded, outData, treeLL, treeDist);
                }
            }
            else /*no LZ77, but still will be Huffman compressed*/
            {
                for (int i = dataPos; i < dataEnd; ++i)
                {
                    BitHelper.AddBitsToStreamReversed(ref bitPointer, outData, treeLL.GetCode(data[i]), (int)treeLL.GetLength(data[i]));
                }
            }
            /*add END code*/
            if (error == 0)
            {
                BitHelper.AddBitsToStreamReversed(ref bitPointer, outData, treeLL.GetCode(256), (int)treeLL.GetLength(256));
            }

            return error;
        }

        private uint DeflateDynamic(byte[] data, List<byte> outData, ref int bitPointer, ref Hash hash, int dataPos, int dataEnd, PNGEncoderSettings settings, byte final)
        {
            uint error = 0;

            /*
            A block is compressed as follows: The PNG data is lz77 encoded, resulting in
            literal bytes and length/distance pairs. This is then huffman compressed with
            two huffman trees. One huffman tree is used for the lit and len values ("ll"),
            another huffman tree is used for the dist values ("d"). These two trees are
            stored using their code lengths, and to compress even more these code lengths
            are also run-length encoded and huffman compressed. This gives a huffman tree
            of code lengths "cl". The code lenghts used to describe this third tree are
            the code length code lengths ("clcl").
            */

            /*The lz77 encoded data, represented with integers since there will also be length and distance codes in it*/
            List<int> lz77Encoded = new List<int>();
            HuffmanTree treeLL = new HuffmanTree(); /*tree for lit,len values*/
            HuffmanTree treeDist = new HuffmanTree(); /*tree for distance codes*/
            HuffmanTree treeCl = new HuffmanTree(); /*tree for encoding the code lengths representing tree_ll and tree_d*/
            uint[] frenquenciesCl; /*frequency of code length codes*/
            List<uint> bitlenLld = new List<uint>(); /*lit,len,dist code lenghts (int bits), literally (without repeat codes).*/
            /*bitlen_lld encoded with repeat codes (this is a rudimentary run length compression)*/
            List<int> bitlenLldE = new List<int>();
            /*bitlen_cl is the code length code lengths ("clcl"). The bit lengths of codes to represent tree_cl
            (these are written as is in the file, it would be crazy to compress these using yet another huffman
            tree that needs to be represented by yet another set of code lengths)*/
            List<int> bitlenCl = new List<int>();
            int dataSize = dataEnd - dataPos;

            byte BFINAL = final;
            int numCodesLL, numCodesDist;
            uint HLIT, HDIST, HCLEN;

            /*This while loop never loops due to a break at the end, it is here to
            allow breaking out of it to the cleanup phase on error conditions.*/
            while (error == 0)
            {
                if (settings.UseLZ77)
                {
                    error = EncodeLZ77(data, dataPos, dataEnd, lz77Encoded, hash, (int)settings.CompressionLevel, (int)settings.MinMatch, (int)settings.NiceMatch, settings.LazyMatching);
                    if (error > 0) break;
                }
                else
                {
                    lz77Encoded.AddRange(new int[dataSize]);
                    for (int i = dataPos; i < dataEnd; ++i)
                    {
                        //lz77Encoded.Add(data[i]); /*no LZ77, but still will be Huffman compressed*/
                        lz77Encoded[i - dataPos] = data[i];
                    }
                }

                uint[] frenquenciesLL = new uint[286];
                uint[] frenquenciesDist = new uint[30];

                /*Count the frequencies of lit, len and dist codes*/
                for (int i = 0; i != lz77Encoded.Count; ++i)
                {
                    var symbol = lz77Encoded[i];
                    ++frenquenciesLL[symbol];
                    if (symbol > 256)
                    {
                        var dist = lz77Encoded[i + 2];
                        ++frenquenciesDist[dist];
                        i += 3;
                    }
                }
                frenquenciesLL[256] = 1; /*there will be exactly 1 end code, at the end of the block*/

                /*Make both huffman trees, one for the lit and len codes, one for the dist codes*/
                error = HuffmanTree.MakeFromFrequences(treeLL, frenquenciesLL, 257, frenquenciesLL.Length, 15);
                if (error > 0) break;
                /*2, not 1, is chosen for mincodes: some buggy PNG decoders require at least 2 symbols in the dist tree*/
                error = HuffmanTree.MakeFromFrequences(treeDist, frenquenciesDist, 2, frenquenciesDist.Length, 15);

                if (error > 0) break;

                numCodesLL = (int)treeLL.Numcodes;
                if (numCodesLL > 286) numCodesLL = 286;

                numCodesDist = (int)treeDist.Numcodes;
                if (numCodesDist > 30) numCodesDist = 30;

                /*store the code lengths of both generated trees in bitlen_lld*/
                for (int i = 0; i != numCodesLL; ++i)
                {
                    bitlenLld.Add(treeLL.GetLength(i));
                }
                for (int i = 0; i != numCodesDist; ++i)
                {
                    bitlenLld.Add(treeDist.GetLength(i));
                }

                /*run-length compress bitlen_ldd into bitlen_lld_e by using repeat codes 16 (copy length 3-6 times),
                17 (3-10 zeroes), 18 (11-138 zeroes)*/
                for (int i = 0; i != bitlenLld.Count; ++i)
                {
                    int j = 0; /*amount of repititions*/
                    while (i + j + 1 < bitlenLld.Count && bitlenLld[i + j + 1] == bitlenLld[i])
                    {
                        ++j;
                    }
                    
                    if (bitlenLld[i] == 0 && j >= 2) /*repeat code for zeroes*/
                    {
                        ++j; /*include the first zero*/
                        if (j <= 10) /*repeat code 17 supports max 10 zeroes*/
                        {
                            bitlenLldE.Add(17);
                            bitlenLldE.Add(j - 3);
                        }
                        else /*repeat code 18 supports max 138 zeroes*/
                        {
                            if (j > 138) j = 138;
                            bitlenLldE.Add(18);
                            bitlenLldE.Add(j - 11);
                        }
                        i += (j - 1);
                    }
                    else if (j >= 3) /*repeat code for value other than zero*/
                    {
                        var num = j / 6;
                        var rest = j % 6;
                        bitlenLldE.Add((int)bitlenLld[i]);
                        for (int k = 0; k < num; ++k)
                        {
                            bitlenLldE.Add(16);
                            bitlenLldE.Add(6 - 3);
                        }
                        if (rest >= 3)
                        {
                            bitlenLldE.Add(16);
                            bitlenLldE.Add(rest - 3);
                        }
                        else
                        {
                            j -= rest;
                        }
                        i += j;
                    }
                    else /*too short to benefit from repeat code*/
                    {
                        bitlenLldE.Add((int)bitlenLld[i]);
                    }
                }

                /*generate tree_cl, the huffmantree of huffmantrees*/

                frenquenciesCl = new uint[HuffmanTree.NumCodeLengthCodes];

                for (int i = 0; i != bitlenLldE.Count; ++i)
                {
                    ++frenquenciesCl[bitlenLldE[i]];

                    /*after a repeat code come the bits that specify the number of repetitions,
                    those don't need to be in the frequencies_cl calculation*/
                    if (bitlenLldE[i] >= 16)
                    {
                        ++i;
                    }
                }

                error = HuffmanTree.MakeFromFrequences(treeCl, frenquenciesCl, frenquenciesCl.Length, frenquenciesCl.Length, 7);
                if (error > 0) break;

                var bitlenClTemp = new int[treeCl.Numcodes];
                for (int i = 0; i != treeCl.Numcodes; ++i)
                {
                    /*lenghts of code length tree is in the order as specified by deflate*/
                    bitlenClTemp[i] = (int)treeCl.GetLength((int)HuffmanTree.ClclOrder[i]);
                }
                bitlenCl = new List<int>(bitlenClTemp);
                while (bitlenCl[bitlenCl.Count - 1] == 0 && bitlenCl.Count > 4)
                {
                    /*remove zeros at the end, but minimum size must be 4*/
                    bitlenCl.RemoveAt(bitlenCl.Count - 1);
                }

                if (error > 0) break;

                /*
                Write everything into the output
                After the BFINAL and BTYPE, the dynamic block consists out of the following:
                - 5 bits HLIT, 5 bits HDIST, 4 bits HCLEN
                - (HCLEN+4)*3 bits code lengths of code length alphabet
                - HLIT + 257 code lenghts of lit/length alphabet (encoded using the code length
                alphabet, + possible repetition codes 16, 17, 18)
                - HDIST + 1 code lengths of distance alphabet (encoded using the code length
                alphabet, + possible repetition codes 16, 17, 18)
                - compressed data
                - 256 (end code)
                */

                BitHelper.AddBitToStream(ref bitPointer, outData, BFINAL);
                BitHelper.AddBitToStream(ref bitPointer, outData, 0); /*first bit of BTYPE "dynamic"*/
                BitHelper.AddBitToStream(ref bitPointer, outData, 1); /*second bit of BTYPE "dynamic"*/

                /*write the HLIT, HDIST and HCLEN values*/
                HLIT = (uint)(numCodesLL - 257);
                HDIST = (uint)(numCodesDist - 1);
                HCLEN = (uint)(bitlenCl.Count - 4);
                /*trim zeroes for HCLEN. HLIT and HDIST were already trimmed at tree creation*/
                while (bitlenCl[(int)HCLEN + 4 - 1] == 0 && HCLEN > 0) --HCLEN;
                BitHelper.AddBitsToStream(ref bitPointer, outData, HLIT, 5);
                BitHelper.AddBitsToStream(ref bitPointer, outData, HDIST, 5);
                BitHelper.AddBitsToStream(ref bitPointer, outData, HCLEN, 4);

                /*write the code lenghts of the code length alphabet*/
                for (int i = 0; i != HCLEN + 4; ++i)
                {
                    BitHelper.AddBitsToStream(ref bitPointer, outData, (uint)bitlenCl[i], 3);
                }

                /*write the lenghts of the lit/len AND the dist alphabet*/
                for (int i = 0; i != bitlenLldE.Count; ++i)
                {
                    BitHelper.AddBitsToStreamReversed(ref bitPointer, outData, treeCl.GetCode(bitlenLldE[i]), (int)treeCl.GetLength(bitlenLldE[i]));
                    /*extra bits of repeat codes*/
                    if (bitlenLldE[i] == 16)
                    {
                        BitHelper.AddBitsToStream(ref bitPointer, outData, (uint)bitlenLldE[++i], 2);
                    }
                    else if (bitlenLldE[i] == 17)
                    {
                        BitHelper.AddBitsToStream(ref bitPointer, outData, (uint)bitlenLldE[++i], 3);
                    }
                    else if (bitlenLldE[i] == 18)
                    {
                        BitHelper.AddBitsToStream(ref bitPointer, outData, (uint)bitlenLldE[++i], 7);
                    }
                }

                /*write the compressed data symbols*/
                WriteLZ77Data(ref bitPointer, lz77Encoded, outData, treeLL, treeDist);

                /*error: the length of the end code 256 must be larger than 0*/
                if (treeLL.GetLength(256) == 0) return 64;

                /*write the end code*/
                BitHelper.AddBitsToStreamReversed(ref bitPointer, outData, treeLL.GetCode(256), (int)treeLL.GetLength(256));

                break;
            }


            return error;
        }

        private uint DeflateNoCompression(byte[] data, List<byte> outData)
        {
            int numDeflateBlocks = (data.Length + ushort.MaxValue - 1) / ushort.MaxValue;
            int datapos = 0;
            for (int i = 0; i != numDeflateBlocks; ++i)
            {
                uint BFINAL, BTYPE, LEN, NLEN;
                byte firstbyte;

                BFINAL = Convert.ToUInt32(i == numDeflateBlocks - 1);
                BTYPE = 0;

                firstbyte = (byte)(BFINAL + ((BTYPE & 1) << 1) + ((BTYPE & 2) << 1));
                outData.Add(firstbyte);

                LEN = ushort.MaxValue;
                if (data.Length - datapos < ushort.MaxValue)
                {
                    LEN = (uint)(data.Length - datapos);
                }
                NLEN = ushort.MaxValue - LEN;

                outData.Add((byte)(LEN & 255));
                outData.Add((byte)(LEN >> 8));
                outData.Add((byte)(NLEN & 255));
                outData.Add((byte)(NLEN >> 8));

                for (int j = 0; j < ushort.MaxValue && datapos < data.Length; ++j)
                {
                    outData.Add(data[datapos++]);
                }
            }

            return 0;
        }

        #endregion

        #region Decompressor

        private uint ReadInt32FromArray(byte[] buffer, int startIndex)
        {
            return (uint)((buffer[startIndex] << 24) | (buffer[startIndex + 1] << 16) | (buffer[startIndex + 2] << 8) | buffer[startIndex + 3]);
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
            for (int i = 0; i != count; ++i)
            {
                result += (uint)((data[bitPointer >> 3] >> (bitPointer & 0x7)) & 1) << i;
                ++bitPointer;
            }
            return result;
        }

        public uint Decompress(byte[] inputData, PNGDecoderSettings settings, List<byte> outData)
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

            if (!settings.IgnoreAdler32 && error == 0)
            {
                var ADLER32 = ReadInt32FromArray(inputData, inputData.Length - 4);
                var checksum = Adler32.GetAdler32(outData.ToArray());

                if (checksum != ADLER32)
                {
                    /*error, adler checksum not correct, data must be corrupted*/
                    return 58;
                }
            }

            return error;
        }

        private uint Inflate(byte[] inputData, List<byte> outData)
        {
            /*bit pointer in the "in" data, current byte is bp >> 3, current bit is bp & 0x7 (from lsb to msb of the byte)*/
            int bp = 0;
            byte BFINAL = 0;
            int pos = 0; /*byte position in the out buffer*/
            uint error = 0;

            while (BFINAL == 0)
            {
                uint BTYPE;
                if (bp + 2 >= inputData.Length * 8)
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
            if (position + 4 >= inputData.Length)
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
                        for (int i = 0; i < length; ++i)
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

            /*code length code lengths ("clcl"), the bit lengths of the huffman tree used to compress bitlen_ll and bitlen_d*/
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

            HuffmanTree treeCl = new HuffmanTree();

            while (error == 0)
            {
                /*read the code length codes out of 3 * (amount of code length codes) bits*/
                uint[] bitlenCl = new uint[HuffmanTree.NumCodeLengthCodes];

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
                uint[] bitlenLL = new uint[HuffmanTree.NumDeflateCodeSymbols];
                uint[] bitlenDist = new uint[HuffmanTree.NumDistanceSymbols];

                i = 0;
                while (i < HLIT + HDIST)
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
                        for (n = 0; n < repLength; ++n)
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

                        for (n = 0; n < repLength; ++n)
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
                        for (n = 0; n < repLength; ++n)
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

        #endregion
    }
}
