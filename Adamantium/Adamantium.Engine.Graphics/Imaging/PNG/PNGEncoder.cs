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

            numDeflateBlocks = 

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
