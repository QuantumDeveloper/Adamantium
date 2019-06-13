using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class Hash
    {
        public const int HashNumValues = ushort.MaxValue + 1;
        public const int HashBitMask = ushort.MaxValue; /*HASH_NUM_VALUES - 1, but C90 does not like that as initializer*/
        public const int MaxSupportedDeflateLength = 258;

        /*hash value to head circular pos - can be outdated if went around window*/
        public int[] Head { get; set; }

        /*circular pos to prev circular pos*/
        public ushort[] Chain { get; set; }

        /*circular pos to hash value*/
        public int[] Val { get; set; }

        /*TODO: do this not only for zeros but for any repeated byte. However for PNG
        it's always going to be the zeros that dominate, so not important for PNG*/

        /*similar to head, but for chainz*/
        public int[] Headz { get; set; }

        /*those with same amount of zeros*/
        public ushort[] Chainz { get; set; }

        /*length of zeros streak, used as a second hash chain*/
        public ushort[] Zeros { get; set; }

        public void Initialize(int windowSize)
        {
            Head = new int[HashNumValues];
            Val = new int[windowSize];
            Chain = new ushort[windowSize];

            Zeros = new ushort[windowSize];
            Headz = new int[MaxSupportedDeflateLength+1];
            Chainz = new ushort[windowSize];

            int i;
            for (i = 0; i != HashNumValues; ++i) Head[i] = -1;
            for (i = 0; i != windowSize; ++i) Val[i] = -1;
            for (i = 0; i != windowSize; ++i) Chain[i] = (ushort)i; /*same value as index indicates uninitialized*/

            for (i = 0; i <= MaxSupportedDeflateLength; ++i) Headz[i] = -1;
            for (i = 0; i != windowSize; ++i) Chainz[i] = (ushort)i; /*same value as index indicates uninitialized*/
        }

        public int GetHash(byte[] data, int pos)
        {
            int result = 0;
            if (pos + 2 < data.Length)
            {
                /*A simple shift and xor hash is used. Since the data of PNGs is dominated
                by zeroes due to the filters, a better hash does not have a significant
                effect on speed in traversing the chain, and causes more time spend on
                calculating the hash.*/

                result ^= (data[pos + 0] << 0);
                result ^= (data[pos + 1] << 4);
                result ^= (data[pos + 2] << 8);
            }
            else
            {
                int amount;
                if (pos >= data.Length) return 0;

                amount = data.Length - pos;
                for (int i = 0; i != amount; ++i)
                {
                    result ^= (data[pos + i] << (i * 8));
                }
            }

            return result & HashBitMask;
        }

        public uint CountZeros(byte[] data, int size, int pos)
        {
            int start = pos;
            int end = start + MaxSupportedDeflateLength;
            if (end > size)
            {
                end = size;
            }

            bool proceed = true;
            int count = 0;
            while(proceed)
            {
                if (data[start] == 0 && start != end)
                {
                    start++;
                    count++;
                }
                else
                {
                    proceed = false;
                }
            }

            return (uint)count;
        }

        /*wpos = pos & (windowsize - 1)*/
        public void UpdateHashChain(int wpos, int hashval, ushort numzeros)
        {
            Val[wpos] = hashval;
            if (Head[hashval] != -1)
            {
                Chain[wpos] = (ushort)Head[hashval];
            }
            Head[hashval] = wpos;

            Zeros[wpos] = numzeros;
            if (Headz[numzeros] != -1)
            {
                Chainz[wpos] = (ushort)Headz[numzeros];
            }
            Headz[numzeros] = wpos;
        }
    }
}
