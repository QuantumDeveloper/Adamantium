using System;
using System.Runtime.CompilerServices;

namespace Adamantium.Imaging.Png;

// DEFLATE (RFC 1951) into a growable array: a 64-bit bit buffer and table-driven Huffman decoding. Error codes follow
// the lodepng numbering the rest of the decoder uses.
internal ref struct Inflater
{
    private const int FastBits = 10;
    private const int FastMask = (1 << FastBits) - 1;
    private const int MaxBits = 15;

    private static readonly ushort[] LengthBase =
        [3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 15, 17, 19, 23, 27, 31, 35, 43, 51, 59, 67, 83, 99, 115, 131, 163, 195, 227, 258];

    private static readonly byte[] LengthExtra =
        [0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 0];

    private static readonly ushort[] DistanceBase =
    [
        1, 2, 3, 4, 5, 7, 9, 13, 17, 25, 33, 49, 65, 97, 129, 193, 257, 385, 513, 769, 1025, 1537, 2049, 3073, 4097,
        6145, 8193, 12289, 16385, 24577
    ];

    private static readonly byte[] DistanceExtra =
        [0, 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7, 8, 8, 9, 9, 10, 10, 11, 11, 12, 12, 13, 13];

    private static readonly byte[] CodeLengthOrder = [16, 17, 18, 0, 8, 7, 9, 6, 10, 5, 11, 4, 12, 3, 13, 2, 14, 1, 15];

    private static readonly Huffman FixedLitLen = BuildFixed(288, i => i <= 143 ? 8 : i <= 255 ? 9 : i <= 279 ? 7 : 8);
    private static readonly Huffman FixedDistance = BuildFixed(30, _ => 5);

    private readonly ReadOnlySpan<byte> input;
    private int position;
    private ulong bits;
    private int bitCount;
    // Zero bits appended past the end of the input; consuming into them means the stream was truncated.
    private int padding;

    private byte[] output;
    private int length;

    private Inflater(ReadOnlySpan<byte> input, byte[] output)
    {
        this.input = input;
        this.output = output;
    }

    /// <summary>Inflates <paramref name="input"/> into <paramref name="output"/>, growing it when needed; returns 0 or
    /// a lodepng error code.</summary>
    public static uint Inflate(ReadOnlySpan<byte> input, ref byte[] output, out int length)
    {
        var inflater = new Inflater(input, output);
        var error = inflater.Run();
        output = inflater.output;
        length = inflater.length;
        return error;
    }

    private uint Run()
    {
        Huffman litLen = null;
        Huffman distance = null;
        Huffman codeLength = null;

        uint final;
        do
        {
            final = GetBits(1);
            var type = GetBits(2);
            uint error;

            switch (type)
            {
                case 0:
                    error = Stored();
                    break;
                case 1:
                    error = Block(FixedLitLen, FixedDistance);
                    break;
                case 2:
                    litLen ??= new Huffman(288);
                    distance ??= new Huffman(32);
                    codeLength ??= new Huffman(19);
                    error = ReadDynamicTrees(litLen, distance, codeLength);
                    if (error == 0)
                    {
                        error = Block(litLen, distance);
                    }
                    break;
                default:
                    return 20;
            }

            if (error != 0)
            {
                return error;
            }

            if (bitCount < padding)
            {
                return 52;
            }
        }
        while (final == 0);

        return 0;
    }

    private uint Stored()
    {
        var drop = bitCount & 7;
        bits >>= drop;
        bitCount -= drop;

        var len = (int)GetBits(16);
        var nlen = (int)GetBits(16);
        if (bitCount < padding)
        {
            return 52;
        }

        if (len != (~nlen & 0xFFFF))
        {
            return 21;
        }

        EnsureCapacity(len);

        // Whole bytes still in the bit buffer come first, then the rest straight from the input.
        while (len > 0 && bitCount - padding >= 8)
        {
            output[length++] = (byte)bits;
            bits >>= 8;
            bitCount -= 8;
            len--;
        }

        if (len == 0)
        {
            return 0;
        }

        if (bitCount != 0 || position + len > input.Length)
        {
            return 52;
        }

        input.Slice(position, len).CopyTo(output.AsSpan(length));
        position += len;
        length += len;
        return 0;
    }

    // Optimized from the first call: an image is decoded once, and tiering would leave most of it unoptimized.
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private uint Block(Huffman litLen, Huffman distance)
    {
        while (true)
        {
            var symbol = Decode(litLen);
            if (symbol < 256)
            {
                if (symbol < 0)
                {
                    return 11;
                }

                if (length == output.Length)
                {
                    EnsureCapacity(1);
                }

                output[length++] = (byte)symbol;
                continue;
            }

            if (symbol == 256)
            {
                return bitCount < padding ? 10u : 0u;
            }

            symbol -= 257;
            if (symbol >= 29)
            {
                return 11;
            }

            var runLength = LengthBase[symbol] + (int)GetBits(LengthExtra[symbol]);

            var distanceSymbol = Decode(distance);
            if (distanceSymbol < 0)
            {
                return 11;
            }

            if (distanceSymbol >= 30)
            {
                return 18;
            }

            var back = DistanceBase[distanceSymbol] + (int)GetBits(DistanceExtra[distanceSymbol]);
            if (bitCount < padding)
            {
                return 51;
            }

            if (back > length)
            {
                return 52;
            }

            EnsureCapacity(runLength);
            var from = length - back;
            if (back >= runLength)
            {
                output.AsSpan(from, runLength).CopyTo(output.AsSpan(length));
            }
            else
            {
                // Overlapping: the run repeats bytes it is still producing.
                for (var i = 0; i < runLength; i++)
                {
                    output[length + i] = output[from + i];
                }
            }

            length += runLength;
        }
    }

    private uint ReadDynamicTrees(Huffman litLen, Huffman distance, Huffman codeLength)
    {
        var hlit = (int)GetBits(5) + 257;
        var hdist = (int)GetBits(5) + 1;
        var hclen = (int)GetBits(4) + 4;
        if (bitCount < padding)
        {
            return 49;
        }

        Span<byte> lengths = stackalloc byte[320];
        lengths.Clear();

        for (var i = 0; i < hclen; i++)
        {
            lengths[CodeLengthOrder[i]] = (byte)GetBits(3);
        }

        var error = codeLength.Build(lengths[..19]);
        if (error != 0)
        {
            return error;
        }

        lengths.Clear();
        var count = hlit + hdist;
        var index = 0;
        while (index < count)
        {
            var symbol = Decode(codeLength);
            if (symbol < 0 || bitCount < padding)
            {
                return 16;
            }

            if (symbol < 16)
            {
                lengths[index++] = (byte)symbol;
                continue;
            }

            byte value = 0;
            int repeat;
            if (symbol == 16)
            {
                if (index == 0)
                {
                    return 54;
                }

                value = lengths[index - 1];
                repeat = 3 + (int)GetBits(2);
            }
            else if (symbol == 17)
            {
                repeat = 3 + (int)GetBits(3);
            }
            else
            {
                repeat = 11 + (int)GetBits(7);
            }

            if (index + repeat > count)
            {
                return symbol == 16 ? 13u : symbol == 17 ? 14u : 15u;
            }

            lengths.Slice(index, repeat).Fill(value);
            index += repeat;
        }

        if (lengths[256] == 0)
        {
            return 64;
        }

        error = litLen.Build(lengths[..hlit]);
        if (error != 0)
        {
            return error;
        }

        return distance.Build(lengths.Slice(hlit, hdist));
    }

    private int Decode(Huffman huffman)
    {
        if (bitCount < MaxBits)
        {
            Refill();
        }

        var entry = huffman.Fast[(int)(bits & FastMask)];
        if (entry != 0)
        {
            var codeLength = entry >> 16;
            bits >>= codeLength;
            bitCount -= codeLength;
            // A truncated stream decodes zero padding for ever.
            return bitCount < padding ? -1 : entry & 0xFFFF;
        }

        var symbol = DecodeLong(huffman);
        return bitCount < padding ? -1 : symbol;
    }

    // Codes longer than the fast table, canonically, a bit at a time.
    private int DecodeLong(Huffman huffman)
    {
        int code = 0, first = 0, index = 0;
        for (var len = 1; len <= MaxBits; len++)
        {
            code |= (int)GetBits(1);
            int count = huffman.Counts[len];
            if (code - count < first)
            {
                return huffman.Symbols[index + (code - first)];
            }

            index += count;
            first = (first + count) << 1;
            code <<= 1;
        }

        return -1;
    }

    private uint GetBits(int count)
    {
        if (count == 0)
        {
            return 0;
        }

        if (bitCount < count)
        {
            Refill();
        }

        var value = (uint)(bits & ((1UL << count) - 1));
        bits >>= count;
        bitCount -= count;
        return value;
    }

    private void Refill()
    {
        while (bitCount <= 56)
        {
            if (position < input.Length)
            {
                bits |= (ulong)input[position++] << bitCount;
            }
            else
            {
                padding += 8;
            }

            bitCount += 8;
        }
    }

    private void EnsureCapacity(int more)
    {
        if (length + more <= output.Length)
        {
            return;
        }

        Array.Resize(ref output, Math.Max(length + more, output.Length * 2));
    }

    private static Huffman BuildFixed(int count, Func<int, int> lengthOf)
    {
        Span<byte> lengths = stackalloc byte[count];
        for (var i = 0; i < count; i++)
        {
            lengths[i] = (byte)lengthOf(i);
        }

        var huffman = new Huffman(count);
        huffman.Build(lengths);
        return huffman;
    }

    private sealed class Huffman
    {
        // Indexed by the next FastBits of input: symbol | (code length << 16), 0 for a longer code.
        public readonly int[] Fast = new int[1 << FastBits];
        public readonly short[] Counts = new short[MaxBits + 1];
        public readonly short[] Symbols;

        public Huffman(int symbols)
        {
            Symbols = new short[symbols];
        }

        public uint Build(ReadOnlySpan<byte> lengths)
        {
            Array.Clear(Fast);
            Array.Clear(Counts);

            foreach (var len in lengths)
            {
                Counts[len]++;
            }

            Counts[0] = 0;

            var left = 1;
            for (var len = 1; len <= MaxBits; len++)
            {
                left = (left << 1) - Counts[len];
                if (left < 0)
                {
                    return 55;
                }
            }

            Span<int> offsets = stackalloc int[MaxBits + 2];
            Span<int> next = stackalloc int[MaxBits + 1];
            var code = 0;
            for (var len = 1; len <= MaxBits; len++)
            {
                offsets[len + 1] = offsets[len] + Counts[len];
                code = (code + Counts[len - 1]) << 1;
                next[len] = code;
            }

            for (var symbol = 0; symbol < lengths.Length; symbol++)
            {
                int len = lengths[symbol];
                if (len == 0)
                {
                    continue;
                }

                Symbols[offsets[len]++] = (short)symbol;

                var canonical = next[len]++;
                if (len > FastBits)
                {
                    continue;
                }

                // Deflate packs a code from its first bit on, so the table is indexed by the code reversed.
                var reversed = 0;
                for (var b = 0; b < len; b++)
                {
                    reversed |= ((canonical >> b) & 1) << (len - 1 - b);
                }

                for (var slot = reversed; slot < Fast.Length; slot += 1 << len)
                {
                    Fast[slot] = symbol | (len << 16);
                }
            }

            return 0;
        }
    }
}
