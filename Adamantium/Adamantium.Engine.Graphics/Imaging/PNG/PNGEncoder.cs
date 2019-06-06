using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public class PNGEncoder
    {
        public void Encode()
        {

        }

        private static void Add32BitInt(List<byte> buffer, uint value)
        {
            buffer.Add((byte)((value >> 24) & 0xff));
            buffer.Add((byte)((value >> 16) & 0xff));
            buffer.Add((byte)((value >> 8) & 0xff));
            buffer.Add((byte)((value >> 0) & 0xff));
        }

        private void AddBitToStream(ref int bitPointer, List<byte> bitStream, byte bit)
        {
            /*add a new byte at the end*/
            if ((bitPointer & 7) == 0)
            {
                bitStream.Add(0);
            }
            /*earlier bit of huffman code is in a lesser significant bit of an earlier byte*/
            bitStream[bitStream.Count - 1] |= (byte)(bit << (bitPointer & 0x7));
            ++bitPointer;
        }

        private void AddBitsToStream(ref int bitPointer, List<byte> bitStream, uint value, int count)
        {
            for (int i = 0; i != count; ++i)
            {
                AddBitToStream(ref bitPointer, bitStream, (byte)((value >> i) & 1));
            }
        }

        private void AddBitsToStreamReversed(ref int bitPointer, List<byte> bitStream, uint value, int count)
        {
            for (int i = 0; i != count; ++i)
            {
                AddBitToStream(ref bitPointer, bitStream, (byte)((value >> (count - 1 - i)) & 1));
            }
        }

        private unsafe uint EncodeLZ77(byte[] inData, int inPos, List<int> outData, Hash hash, 
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

            if (windowSize == 0 || windowSize > short.MaxValue)
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

            for (pos = inPos; pos< inData.Length; ++pos)
            {
                /*position for in 'circular' hash buffers*/
                int wpos = pos & (windowSize - 1);
                int chainLength = 0;

                hashval = hash.GetHash(inData, pos);

                if (useZeros && hashval == 0)
                {
                    if (numZeros == 0)
                    {
                        numZeros = hash.CountZeros(inData, pos);
                    }
                    else if (pos + numZeros > inData.Length || inData[pos + numZeros - 1] != 0)
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

                hashpos = hash.Chain[wpos];

                int index = inData.Length < pos + Hash.MaxSupportedDeflateLength ? inData.Length : pos + Hash.MaxSupportedDeflateLength;
                fixed(byte* lastPtr = &inData[index])
                {
                    prevOffset = 0;
                    for (;;)
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
                            fixed(byte* forePtr = &inData[pos])
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
                        for (int i = 1; i< length; ++i)
                        {
                            ++pos;
                            wpos = pos & (windowSize - 1);
                            hashval = hash.GetHash(inData, pos);
                            if (useZeros && hashval == 0)
                            {
                                if (numZeros == 0)
                                {
                                    numZeros = hash.CountZeros(inData, pos);
                                }
                                else if (pos + numZeros > inData.Length || inData[pos + numZeros - 1] != 0)
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
                AddBitsToStreamReversed(ref bitPointer, outData, treeLL.GetCode(val), (int)treeLL.GetLength(val));
                if (val > 256) /*for a length code, 3 more things have to be added*/
                {
                    var lengthIndex = val - HuffmanTree.FirstLengthCodeIndex;
                    var nLengthExtraBits = (int)HuffmanTree.LengthExtra[lengthIndex];
                    var lengthExtraBits = (uint)lz77Encoded[++i];

                    var distanceCode = lz77Encoded[++i];

                    var distanceIndex = distanceCode;
                    var nDistanceExtraBits = HuffmanTree.DistanceExtra[distanceIndex];
                    var distanceExtraBits = lz77Encoded[++i];

                    AddBitsToStream(ref bitPointer, outData, lengthExtraBits, nLengthExtraBits);
                    AddBitsToStreamReversed(ref bitPointer, outData, treeDist.GetCode(distanceCode), (int)treeDist.GetLength(distanceCode));
                    AddBitsToStream(ref bitPointer, outData, (uint)distanceExtraBits, (int)nDistanceExtraBits);
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

            while(left <= right)
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

        internal uint Compress(byte[] inData, PNGEncoderSettings settings, List<byte> outData)
        {
            uint error = 0;

            /*zlib data: 1 byte CMF (CM+CINFO), 1 byte FLG, deflate data, 4 byte ADLER32 checksum of the Decompressed data*/
            uint CMF = 120; /*0b01111000: CM 8, CINFO 7. With CINFO 7, any window size up to 32768 can be used.*/
            uint FLEVEL = 0;
            uint FDICT = 0;
            uint CMFFLG = 256 * CMF + FDICT * 32 + FLEVEL * 64;
            uint FCHECK = 31 - CMFFLG % 31;
            CMFFLG += FCHECK;

            var deflatedData = new List<byte>();
            error = Deflate(inData, settings, deflatedData);

            if (error == 0)
            {
                uint ADLER32 = Adler32.GetAdler32(inData);
                outData.AddRange(deflatedData);
                Add32BitInt(outData, ADLER32);
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
            else /*if(settings->btype == 2)*/
            {
                /*on PNGs, deflate blocks of 65-262k seem to give most dense encoding*/
                blockSize = inData.Length / 8 + 8;
                if (blockSize < ushort.MaxValue + 1) blockSize = ushort.MaxValue + 1;
                if (blockSize > 262144) blockSize = 262144;
            }

            numDeflateBlocks = (inData.Length + blockSize - 1) / blockSize;
            if (numDeflateBlocks == 0) numDeflateBlocks = 1;

            hash = new Hash();
            hash.Initialize(settings.WindowSize);

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

            AddBitToStream(ref bitPointer, outData, BFINAL);
            AddBitToStream(ref bitPointer, outData, 1); /*first bit of BTYPE*/
            AddBitToStream(ref bitPointer, outData, 0); /*second bit of BTYPE*/

            if (settings.UseLZ77)
            {
                List<int> lz77Encoded = new List<int>();
                error = EncodeLZ77(data, dataPos, lz77Encoded, hash, settings.WindowSize, (int)settings.MinMatch, (int)settings.NiceMatch, settings.LazyMatching);
                if (error == 0)
                {
                    WriteLZ77Data(ref bitPointer, lz77Encoded, outData, treeLL, treeDist);
                }
            }
            else /*no LZ77, but still will be Huffman compressed*/
            {
                for (int i = dataPos; i< dataEnd; ++i)
                {
                    AddBitsToStreamReversed(ref bitPointer, outData, treeLL.GetCode(data[i]), (int)treeLL.GetLength(data[i]));
                }
            }
            /*add END code*/
            if (error == 0)
            {
                AddBitsToStreamReversed(ref bitPointer, outData, treeLL.GetCode(256), (int)treeLL.GetLength(256));
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
            List<uint> frenquenciesLL = new List<uint>(); /*frequency of lit,len codes*/
            List<uint> frenquenciesDist = new List<uint>(); /*frequency of dist codes*/
            List<uint> frenquenciesCl = new List<uint>(); /*frequency of code length codes*/
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
            while(error == 0)
            {
                if (settings.UseLZ77)
                {
                    error = EncodeLZ77(data, dataPos, lz77Encoded, hash, settings.WindowSize, (int)settings.MinMatch, (int)settings.NiceMatch, settings.LazyMatching);
                    if (error > 0) break;
                }
                else
                {
                    for (int i = dataPos; i< dataEnd; ++i)
                    {
                        lz77Encoded.Add(data[i]); /*no LZ77, but still will be Huffman compressed*/
                    }
                }

                /*Count the frequencies of lit, len and dist codes*/
                for(int i = 0; i != lz77Encoded.Count; ++i)
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
                error = HuffmanTree.MakeFromFrequences(treeLL, frenquenciesLL.ToArray(), 257, frenquenciesLL.Count, 15);
                if (error > 0) break;
                /*2, not 1, is chosen for mincodes: some buggy PNG decoders require at least 2 symbols in the dist tree*/
                error = HuffmanTree.MakeFromFrequences(treeDist, frenquenciesDist.ToArray(), 2, frenquenciesLL.Count, 15);

                if (error > 0) break;

                numCodesLL = (int)treeLL.Numcodes;
                if (numCodesLL > 286) numCodesLL = 286;

                numCodesDist = (int)treeDist.Numcodes;
                if (numCodesDist > 30) numCodesDist = 30;

                /*store the code lengths of both generated trees in bitlen_lld*/
                for(int i = 0; i != numCodesLL; ++i)
                {
                    bitlenLld.Add(treeLL.GetLength(i));
                }
                for (int i = 0; i != numCodesDist; ++i)
                {
                    bitlenLld.Add(treeDist.GetLength(i));
                }

                67874+947+947+974797
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
    }
}
