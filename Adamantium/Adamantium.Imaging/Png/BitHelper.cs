using System.Collections.Generic;

namespace Adamantium.Imaging.Png
{
    public static class BitHelper
    {
        internal static unsafe byte ReadBitFromReversedStream(ref int bitPointer, byte* data)
        {
            byte result = (byte)((data[bitPointer >> 3] >> (7 - (bitPointer & 0x7))) & 1);
            ++bitPointer;
            return result;
        }

        internal static unsafe byte ReadBitFromReversedStream(ref int bitPointer, byte[] data)
        {
            fixed (byte* dataPtr = &data[0])
            {
                return ReadBitFromReversedStream(ref bitPointer, dataPtr);
            }
        }

        internal static unsafe uint ReadBitsFromReversedStream(ref int bitPointer, byte[] data, int count)
        {
            fixed (byte* dataPtr = &data[0])
            {
                return ReadBitsFromReversedStream(ref bitPointer, dataPtr, count);
            }
        }

        internal static unsafe uint ReadBitsFromReversedStream(ref int bitPointer, byte* data, int count)
        {
            uint result = 0;
            for (int i = 0; i < count; ++i)
            {
                result <<= 1;
                result |= ReadBitFromReversedStream(ref bitPointer, data);
            }
            return result;
        }

        internal static unsafe void SetBitOfReversedStream(ref int bitPointer, byte[] bitStream, byte bit)
        {
            fixed(byte* bitStreamPtr = &bitStream[0])
            {
                SetBitOfReversedStream(ref bitPointer, bitStreamPtr, bit);
            }
        }

        internal static unsafe void SetBitOfReversedStream(ref int bitPointer, byte* bitStream, byte bit)
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

        internal static void Add32BitInt(List<byte> buffer, uint value)
        {
            buffer.Add((byte)((value >> 24) & 0xff));
            buffer.Add((byte)((value >> 16) & 0xff));
            buffer.Add((byte)((value >> 8) & 0xff));
            buffer.Add((byte)((value >> 0) & 0xff));
        }

        internal static void AddBitToStream(ref int bitPointer, List<byte> bitStream, byte bit)
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

        internal static void AddBitsToStream(ref int bitPointer, List<byte> bitStream, uint value, int count)
        {
            for (int i = 0; i != count; ++i)
            {
                AddBitToStream(ref bitPointer, bitStream, (byte)((value >> i) & 1));
            }
        }

        internal static void AddBitsToStreamReversed(ref int bitPointer, List<byte> bitStream, uint value, int count)
        {
            for (int i = 0; i != count; ++i)
            {
                AddBitToStream(ref bitPointer, bitStream, (byte)((value >> (count - 1 - i)) & 1));
            }
        }
    }
}
