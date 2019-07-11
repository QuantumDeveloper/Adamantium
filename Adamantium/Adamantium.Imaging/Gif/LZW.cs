using System;
using System.Collections.Generic;

namespace Adamantium.Imaging.Gif
{
    internal class LZW
    {
        private int EOF = -1;
        private int MAXBITS = 12;
        private int HSIZE = 5003; // 80% occupancy
        private int[] masks = {0x0000, 0x0001, 0x0003, 0x0007, 0x000F, 0x001F,
                     0x003F, 0x007F, 0x00FF, 0x01FF, 0x03FF, 0x07FF,
                     0x0FFF, 0x1FFF, 0x3FFF, 0x7FFF, 0xFFFF };

        public byte[] Compress(int[] indexData, int colorDepth)
        {
            var outData = new List<byte>();
            int[] htab = new int[HSIZE];
            int[] codetab = new int[HSIZE];
            int hsize = HSIZE; // for dynamic table sizing
            int maxbits = MAXBITS;
            int maxmaxcode = 1 << MAXBITS; // should NEVER generate this code

            int initCodeSize = Math.Max(2, colorDepth) + 1;
            int remaining = indexData.Length;
            int curPixel = 0;

            int cur_accum = 0;
            int cur_bits = 0;

            int fcode;
            int i /* = 0 */;
            int c;
            int disp;
            int hsize_reg;
            int hshift;

            //g_init_bits - initial number of bits
            int g_init_bits = initCodeSize;

            bool clear_flg = false;
            int n_bits = g_init_bits;
            int maxcode = GetMaxCode(n_bits);

            int ClearCode = 1 << colorDepth;
            int EOFCode = ClearCode + 1;
            int free_ent = ClearCode + 2; // first unused entry

            int GetMaxCode(int bits)
            {
                return (1 << bits) - 1;
            }

            void ClearHash(int size)
            {
                for (var i = 0; i < size; ++i) htab[i] = -1;
            }

            int GetNextPixel()
            {
                if (remaining == 0) return EOF;
                --remaining;
                var pixel = indexData[curPixel++];
                return pixel & 0xff;
            }

            void Output(int code, List<byte> outs)
            {
                cur_accum &= masks[cur_bits];

                if (cur_bits > 0)
                {
                    cur_accum |= code << cur_bits;
                }
                else
                {
                    cur_accum = code;
                }

                cur_bits += n_bits;

                while (cur_bits >= 8)
                {
                    outs.Add((byte)(cur_accum & 0xff));
                    cur_accum >>= 8;
                    cur_bits -= 8;
                }

                // If the next entry is going to be too big for the code size,
                // then increase it, if possible.
                if (free_ent > maxcode || clear_flg)
                {
                    if (clear_flg)
                    {
                        maxcode = GetMaxCode(n_bits = g_init_bits);
                        clear_flg = false;
                    }
                    else
                    {
                        ++n_bits;
                        if (n_bits == maxbits)
                        {
                            maxcode = maxmaxcode;
                        }
                        else
                        {
                            maxcode = GetMaxCode(n_bits);
                        }
                    }
                }

                if (code == EOFCode)
                {
                    // At EOF, write the rest of the buffer.
                    while (cur_bits > 0)
                    {
                        outs.Add((byte)(cur_accum & 0xff));
                        cur_accum >>= 8;
                        cur_bits -= 8;
                    }
                }
            }

            void ClearBlock(List<byte> outs)
            {
                ClearHash(hsize);
                free_ent = ClearCode + 2;
                clear_flg = true;

                Output(ClearCode, outs);
            }

            var ent = GetNextPixel();
            hshift = 0;
            for (fcode = hsize; fcode < 65536; fcode *= 2)
            {
                ++hshift;
            }
            hshift = 8 - hshift; // set hash code range bound
            hsize_reg = hsize;
            ClearHash(hsize_reg);

            Output(ClearCode, outData);

        outerLoop:
            while ((c = GetNextPixel()) != EOF)
            {
                fcode = (c << maxbits) + ent;
                i = (c << hshift) ^ ent; // xor hashing

                if (htab[i] == fcode)
                {
                    ent = codetab[i];
                    continue;
                }
                else if (htab[i] >= 0) // non-empty slot
                {
                    disp = hsize_reg - i; // secondary hash (after G. Knott)
                    if (i == 0)
                    {
                        disp = 1;
                    }
                    do
                    {
                        if ((i -= disp) < 0)
                        {
                            i += hsize_reg;
                        }

                        if (htab[i] == fcode)
                        {
                            ent = codetab[i];
                            goto outerLoop;
                        }
                    }
                    while (htab[i] >= 0);
                }
                Output(ent, outData);
                ent = c;
                if (free_ent < maxmaxcode)
                {
                    codetab[i] = free_ent++; // code -> hashtable
                    htab[i] = fcode;
                }
                else
                {
                    ClearBlock(outData);
                }
            }

            Output(ent, outData);
            Output(EOFCode, outData);

            return outData.ToArray();
        }

        public int[] Decompress(byte[] compressedData, int minimumCodeSize)
        {
            uint mask = 0x01;
            int inputLength = compressedData.Length;

            var pos = 0;
            int readCode(int size)
            {
                int code = 0x0;
                for (var i = 0; i < size; i++)
                {
                    var val = compressedData[pos];
                    int bit = (val & mask) != 0 ? 1 : 0;
                    mask <<= 1;

                    if (mask == 0x100)
                    {
                        mask = 0x01;
                        pos++;
                        inputLength--;
                        if (inputLength == 0)
                        {
                            //break here if there is the end of data and still no EOI code (maybe because of buggy encoder)
                            break;
                        }
                    }

                    code |= (bit << i);
                }
                return code;
            };

            var indexStream = new List<int>();

            var clearCode = 1 << minimumCodeSize;
            var eoiCode = clearCode + 1;

            var codeSize = minimumCodeSize + 1;

            var dict = new List<List<int>>();

            void Clear()
            {
                dict.Clear();
                codeSize = minimumCodeSize + 1;
                for (int i = 0; i < clearCode; i++)
                {
                    dict.Add(new List<int>() { i });
                }
                dict.Add(new List<int>());
                dict.Add(null);
            }

            int code = 0x0;
            while (inputLength > 0)
            {
                int last = code;
                code = readCode(codeSize);

                if (code == clearCode)
                {
                    Clear();
                    continue;
                }
                if (code == eoiCode)
                {
                    break;
                }

                if (code < dict.Count)
                {
                    if (last != clearCode)
                    {
                        var lst = new List<int>(dict[last]);
                        lst.Add(dict[code][0]);
                        dict.Add(lst);
                    }
                }
                else
                {
                    //if (last != clearCode)
                    {
                        var lst = new List<int>(dict[last]);
                        lst.Add(dict[last][0]);
                        dict.Add(lst);
                    }
                }

                indexStream.AddRange(dict[code]);

                if (dict.Count == (1 << codeSize) && codeSize < 12)
                {
                    // If we're at the last code and codeSize is 12, the next code will be a clearCode, and it'll be 12 bits long.
                    codeSize++;
                }
            }

            return indexStream.ToArray();

        }
    }
}
