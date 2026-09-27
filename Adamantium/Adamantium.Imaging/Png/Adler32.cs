using System;
using System.Runtime.CompilerServices;

namespace Adamantium.Imaging.Png
{
    public static class Adler32
    {
        public static uint GetAdler32(byte[] data)
        {
            return GetAdler32(1, data);
        }

        public static uint GetAdler32(uint adler, byte[] data)
        {
            return GetAdler32(adler, (ReadOnlySpan<byte>)data);
        }

        public static uint GetAdler32(ReadOnlySpan<byte> data)
        {
            return GetAdler32(1, data);
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static unsafe uint GetAdler32(uint adler, ReadOnlySpan<byte> data)
        {
            uint s1 = adler & 0xffff;
            uint s2 = (adler >> 16) & 0xffff;
            var len = data.Length;

            fixed (byte* start = data)
            {
                var p = start;
                while (len > 0)
                {
                    /*at least 5552 sums can be done before the sums overflow, saving a lot of module divisions*/
                    var amount = len > 5552 ? 5552 : len;
                    len -= amount;

                    while (amount >= 8)
                    {
                        s1 += p[0]; s2 += s1;
                        s1 += p[1]; s2 += s1;
                        s1 += p[2]; s2 += s1;
                        s1 += p[3]; s2 += s1;
                        s1 += p[4]; s2 += s1;
                        s1 += p[5]; s2 += s1;
                        s1 += p[6]; s2 += s1;
                        s1 += p[7]; s2 += s1;
                        p += 8;
                        amount -= 8;
                    }

                    while (amount > 0)
                    {
                        s1 += *p++;
                        s2 += s1;
                        --amount;
                    }

                    s1 %= 65521;
                    s2 %= 65521;
                }
            }

            return (s2 << 16) | s1;
        }
    }
}
