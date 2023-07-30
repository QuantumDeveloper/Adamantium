using System;

//using System.Runtime.InteropServices;

namespace Adamantium.Imaging.Png
{
    internal class HuffmanTree
    {
        public const uint FirstLengthCodeIndex = 257;
        public const uint LastLengthCodeIndex = 285;
        /*256 literals, the end code, some length codes, and 2 unused codes*/
        public const uint NumDeflateCodeSymbols = 288;
        /*the distance codes have their own symbols, 30 used, 2 unused*/
        public const uint NumDistanceSymbols = 32;
        /*the code length codes. 
         * 0-15: code lengths, 
         * 16: copy previous 3-6 times, 
         * 17: 3-10 zeros, 
         * 18: 11-138 zeros*/
        public const uint NumCodeLengthCodes = 19;

        public static uint[] LengthBase { get; }

        public static uint[] LengthExtra { get; }

        public static uint[] DistanceBase { get; }

        public static uint[] DistanceExtra { get; }

        public static uint[] ClclOrder { get; }

        static HuffmanTree()
        {
            LengthBase = new uint[29] 
            {
                3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59,
                67, 83, 99, 115, 131, 163, 195, 227, 258
            };

            LengthExtra = new uint[29]
            {
                0, 0, 0, 0, 0, 0, 0,  0,  1,  1,  1,  1,  2,  2,  2,  2,  3,  3,  3,  3,
                4,  4,  4,   4,   5,   5,   5,   5,   0
            };

            DistanceBase = new uint[30]
            {
                1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513,
                769, 1025, 1537, 2049, 3073, 4097, 6145, 8193, 12289, 16385, 24577
            };

            DistanceExtra = new uint[30]
            {
                0, 0, 0, 0, 1, 1, 2,  2,  3,  3,  4,  4,  5,  5,   6,   6,   7,   7,   8,
                8,    9,    9,   10,   10,   11,   11,   12,    12,    13,    13
            };

            ClclOrder = new uint[]
            {
                16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15
            };
        }

        public uint[] Tree2D { get; set; }
        public uint[] Tree1D { get; set; }

        /*the lengths of the codes of the 1d-tree*/
        public uint[] Lenghts { get; set; }

        /*maximum number of bits a single code can get*/
        public uint MaxBitLen { get; set; }

        /*number of symbols in the alphabet = number of codes*/
        public uint Numcodes { get; set; }

        public uint GetCode(int index)
        {
            return Tree1D[index];
        }

        public uint GetLength(int index)
        {
            return Lenghts[index];
        }

        public static uint Make2DTree(HuffmanTree tree)
        {
            uint nodeFilled = 0; /*up to which node it is filled*/
            uint treepos = 0; /*position in the tree (1 of the numcodes columns)*/
            uint n, i;

            tree.Tree2D = new uint[tree.Numcodes * 2];

            /*
            convert tree1d[] to tree2d[][]. In the 2D array, a value of 32767 means
            uninited, a value >= numcodes is an address to another bit, a value < numcodes
            is a code. The 2 rows are the 2 possible bit values (0 or 1), there are as
            many columns as codes - 1.
            A good huffman tree has N * 2 - 1 nodes, of which N - 1 are internal nodes.
            Here, the internal nodes are stored (what their 0 and 1 option point to).
            There is only memory for such good tree currently, if there are more nodes
            (due to too long length codes), error 55 will happen
            */
            for (n = 0; n < tree.Numcodes * 2; ++n)
            {
                /*32767 here means the tree2d isn't filled there yet*/
                tree.Tree2D[n] = 32767; 
            }

            /*the codes*/
            for (n = 0; n< tree.Numcodes; ++n)
            {
                /*the bits for this code*/
                for (i = 0; i!= tree.Lenghts[n]; ++i)
                {
                    byte bit = (byte)((tree.Tree1D[n] >> (int)(tree.Lenghts[n] - i - 1)) & 1);
                    if (treepos > int.MaxValue || treepos + 2 > tree.Numcodes)
                    {
                        return 55;
                    }
                    if (tree.Tree2D[2 * treepos + bit] == 32767) /*not yet filled in*/
                    {
                        if (i + 1 == tree.Lenghts[n]) /*last bit*/
                        {
                            tree.Tree2D[2 * treepos + bit] = n; /*put the current code in it*/
                            treepos = 0;
                        }
                        else
                        {
                            /*put address of the next step in here, first that address has to be found of course
                            (it's just nodefilled + 1)...*/
                            ++nodeFilled;
                            /*addresses encoded with numcodes added to it*/
                            tree.Tree2D[2 * treepos + bit] = nodeFilled + tree.Numcodes;
                            treepos = nodeFilled;
                        }
                    }
                    else
                    {
                        treepos = tree.Tree2D[2 * treepos + bit] - tree.Numcodes;
                    }
                }
            }

            for (n = 0; n < tree.Numcodes * 2; ++n)
            {
                if (tree.Tree2D[n] == 32767)
                {
                    /*remove possible remaining 32767's*/
                    tree.Tree2D[n] = 0;
                }
            }

            return 0;
        }

        public static uint MakeFromLength(HuffmanTree tree, uint[] bitlen, uint numcodes, uint maxbitlen )
        {
            uint i;
            //tree.Lenghts = new uint[numcodes * Marshal.SizeOf<uint>()];
            tree.Lenghts = new uint[numcodes];
            for (i = 0; i != numcodes; ++i)
            {
                tree.Lenghts[i] = bitlen[i];
            }
            tree.Numcodes = numcodes; /*number of symbols*/
            tree.MaxBitLen = maxbitlen;
            return MakeFromLength2(tree);
        }

        public static uint MakeFromLength2(HuffmanTree tree)
        {
            uint[] blCount = new uint[tree.MaxBitLen + 1];
            uint[] nextCode = new uint[tree.MaxBitLen + 1];
            uint error = 0;
            uint bits, n;

            //tree.Tree1D = new uint[tree.Numcodes * Marshal.SizeOf<uint>()];
            tree.Tree1D = new uint[tree.Numcodes];

            /*step 1: count number of instances of each code length*/
            for (bits = 0; bits != tree.Numcodes; ++bits)
            {
                ++blCount[tree.Lenghts[bits]];
            }
            /*step 2: generate the nextcode values*/
            for (bits = 1; bits <= tree.MaxBitLen; ++bits)
            {
                nextCode[bits] = (nextCode[bits - 1] + blCount[bits - 1] << 1);
            }
            /*step 3: generate all the codes*/
            for (n = 0; n != tree.Numcodes; ++n)
            {
                if (tree.Lenghts[n] != 0)
                {
                    tree.Tree1D[n] = nextCode[tree.Lenghts[n]]++;
                }
            }

            return Make2DTree(tree);
        }

        public static void GenerateFixedLitLenTree(HuffmanTree tree)
        {
            int i = 0;
            uint error = 0;
            //uint[] bitlen = new uint[NumDeflateCodeSymbols * Marshal.SizeOf<uint>()];
            uint[] bitlen = new uint[NumDeflateCodeSymbols];
            /*288 possible codes: 0-255=literals, 256=endcode, 257-285=lengthcodes, 286-287=unused*/
            for (i = 0; i <= 143; ++i) bitlen[i] = 8;
            for (i = 144; i <= 255; ++i) bitlen[i] = 9;
            for (i = 256; i <= 279; ++i) bitlen[i] = 7;
            for (i = 280; i <= 287; ++i) bitlen[i] = 8;

            error = MakeFromLength(tree, bitlen, NumDeflateCodeSymbols, 15);

            Array.Clear(bitlen, 0, bitlen.Length);
            bitlen = null;
        }

        public static uint GenerateFixedDistanceTree(HuffmanTree tree)
        {
            uint[] bitlen = new uint[NumDistanceSymbols];

            for (int i = 0; i!= NumDistanceSymbols; ++i)
            {
                bitlen[i] = 5;
            }

            return MakeFromLength(tree, bitlen, NumDistanceSymbols, 15);
        }

        /*
        returns the code, or (unsigned)(-1) if error happened
        bitlength is the length of the complete buffer, in bits (so its byte length times 8)
        */
        public static int DecodeSymbol(byte[] inputData, ref int bitPointer, HuffmanTree codeTree, int bitLength)
        {
            uint treepos = 0, ct;
            for (; ; )
            {
                if (bitPointer >= bitLength)
                {
                    /*error: end of input memory reached without endcode*/
                    return -1;
                }
                /*
                decode the symbol from the tree. The "readBitFromStream" code is inlined in
                the expression below because this is the biggest bottleneck while decoding
                */
                ct = codeTree.Tree2D[(treepos << 1) + (uint)((inputData[bitPointer >> 3] >> (bitPointer & 0x7)) & 1)];
                ++bitPointer;
                if (ct < codeTree.Numcodes)
                {
                    /*the symbol is decoded, return it*/
                    return (int)ct;
                }
                else
                {
                    /*symbol not yet decoded, instead move tree position*/
                    treepos = ct - codeTree.Numcodes;
                }

                if (treepos >= codeTree.Numcodes)
                {
                    /*error: it appeared outside the codetree*/
                    return -1;
                }
            }
        }

        public static uint MakeFromFrequences(HuffmanTree tree, uint[] frequencies, int mincodes, int numcodes, uint maxbitlen)
        {
            uint error = 0;

            while (frequencies[numcodes - 1] == 0 && numcodes > mincodes) --numcodes;

            tree.MaxBitLen = maxbitlen;
            tree.Numcodes = (uint)numcodes; /*number of symbols*/
            tree.Lenghts = new uint[numcodes];

            error = CodeLength(tree.Lenghts, frequencies, numcodes, maxbitlen);
            if (error == 0)
            {
                error = MakeFromLength2(tree);
            }

            return error;
        }

        public static uint CodeLength(uint[] lengths, uint[] freauencies, int numcodes, uint maxbitlen)
        {
            uint error = 0;

            int numpresent = 0; /*number of symbols with non-zero frequency*/
            BPMNode[] leaves; /*the symbols, only those with > 0 frequency*/

            if (numcodes == 0)
            {
                /*error: a tree of 0 symbols is not supposed to be made*/
                return 80;
            }

            if ((1 << (int)maxbitlen) < numcodes)
            {
                /*error: represent all symbols*/
                return 80;
            }

            leaves = new BPMNode[numcodes];

            for (int i = 0; i != numcodes; ++i)
            {
                if (freauencies[i] > 0)
                {
                    leaves[numpresent] = new BPMNode();
                    leaves[numpresent].Weight = (int)freauencies[i];
                    leaves[numpresent].Index = i;
                    ++numpresent;
                }
            }

            for (int i = 0; i != numcodes; ++i) lengths[i] = 0;

            /*ensure at least two present symbols. There should be at least one symbol
            according to RFC 1951 section 3.2.7. Some decoders incorrectly require two. To
            make these work as well ensure there are at least two symbols. The
            Package-Merge code below also doesn't work correctly if there's only one
            symbol, it'd give it the theoritical 0 bits but in practice zlib wants 1 bit*/

            if (numpresent == 0)
            {
                /*note that for RFC 1951 section 3.2.7, only lengths[0] = 1 is needed*/
                lengths[0] = lengths[1] = 1;
            }
            else if (numpresent == 1)
            {
                lengths[leaves[0].Index] = 1;
                lengths[leaves[0].Index == 0 ? 1 : 0] = 1;
            }
            else
            {
                BpmLists lists = new BpmLists();
                BPMNode node = null;

                BPMNode.Sort(ref leaves, numpresent);
                lists.ListSize = maxbitlen;
                lists.MemSize = 2 * maxbitlen * (maxbitlen + 1);
                lists.NextFree = 0;
                lists.Memory = new BPMNode[lists.MemSize];
                lists.FreeList = new BPMNode[lists.MemSize];
                lists.Chains0 = new BPMNode[lists.ListSize];
                lists.Chains1 = new BPMNode[lists.ListSize];

                for (int i = 0; i != lists.MemSize; ++i)
                {
                    lists.FreeList[i] = lists.Memory[i];
                }

                BPMNode.Create(lists, leaves[0].Weight, 1, null);
                BPMNode.Create(lists, leaves[1].Weight, 2, null);

                for (int i = 0; i != lists.ListSize; ++i)
                {
                    lists.Chains0[i] = lists.Memory[0];
                    lists.Chains1[i] = lists.Memory[1];
                }

                /*each boundaryPM call adds one chain to the last list, and we need 2 * numpresent - 2 chains.*/
                for (int i = 2; i != 2 * numpresent -2; ++i)
                {
                    BPMNode.BoundaryPackageMerge(lists, leaves, numpresent, (int)maxbitlen - 1, i);
                }

                for (node = lists.Chains1[maxbitlen - 1]; node != null; node = node.Tail)
                {
                    for (int i = 0; i != node.Index; ++i)
                    {
                        ++lengths[leaves[i].Index];
                    }
                }
            }

            return error;
        }

        
    }
}
