using System;
using System.IO;
using System.Linq;
using Adamantium.Imaging.Png.Chunks;

namespace Adamantium.Imaging.Png.IO
{
    internal class PNGStreamWriter : MemoryStream
    {
        internal static byte[] PngHeader = { 137, 80, 78, 71, 13, 10, 26, 10 };

        public void WriteBytes(byte[] bytes)
        {
            for (int i = 0; i < bytes.Length; ++i)
            {
                WriteByte(bytes[i]);
            }
        }

        public void WriteUInt16(ushort value)
        {
            var bytes = BitConverter.GetBytes(value);
            WriteBytes(bytes.Reverse().ToArray());
        }

        public void WriteUInt32(uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            WriteBytes(bytes.Reverse().ToArray());
        }

        public void WriteInt32(int value)
        {
            var bytes = BitConverter.GetBytes(value);
            WriteBytes(bytes.Reverse().ToArray());
        }

        /*CHUNKS*/
        internal void WriteSignature()
        {
            WriteBytes(PngHeader);
        }

        internal void WriteIHDR(PngState state, int width, int height)
        {
            var bytes = IHDR.FromState(state, width, height).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WriteIDAT(PngState state, byte[] rawBytes)
        {
            var bytes = IDAT.FromState(state, rawBytes).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritebKGD(PngState state)
        {
            var bytes = bKGD.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritecHRM(PngState state)
        {
            var bytes = cHRM.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritegAMA(PngState state)
        {
            var bytes = gAMA.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WriteiCCP(PngState state)
        {
            var bytes = iCCP.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WriteIEND(PngState state)
        {
            var bytes = new IEND().GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WriteiTXt(PngState state)
        {
            foreach (var item in state.InfoPng.ITextItems)
            {
                var bytes = iTXt.FromTextItem(item).GetChunkBytes(state);
                WriteChunk(bytes);
            }
        }

        internal void WritetEXt(PngState state)
        {
            foreach (var item in state.InfoPng.TextItems)
            {
                var bytes = tEXt.FromTextItem(item).GetChunkBytes(state);
                WriteChunk(bytes);
            }
        }

        internal void WritezTXt(PngState state)
        {
            if (state.EncoderSettings.TextCompression)
            {
                foreach (var item in state.InfoPng.TextItems)
                {
                    var bytes = zTXt.FromTextItem(item).GetChunkBytes(state);
                    WriteChunk(bytes);
                }
            }
        }

        internal void WritepHYs(PngState state)
        {
            var bytes = pHYs.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritePLTE(PngState state)
        {
            var bytes = PLTE.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritesRGB(PngState state)
        {
            var bytes = sRGB.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritetIME(PngState state)
        {
            var bytes = tIME.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritetRNS(PngState state)
        {
            var bytes = tRNS.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        /*Animated PNG*/
        internal void WriteacTL(PngState state)
        {
            var bytes = acTL.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritefcTL(PngFrame frame)
        {
            var bytes = fcTL.FromFrame(frame).GetChunkBytes(null);
            WriteChunk(bytes);
        }

        internal void WritefdAT(byte[] rawData, uint sequenceNumber, PngState state)
        {
            var bytes = new fdAT() { FrameData = rawData, SequenceNumber = sequenceNumber }.GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WriteChunk(byte[] chunkBytes)
        {
            WriteUInt32((uint)(chunkBytes.Length - 4));
            WriteBytes(chunkBytes);
            CalculateAndWriteCRC(chunkBytes);
        }

        internal void CalculateAndWriteCRC(byte[] chunkBytes)
        {
            var crc = Crc32.CalculateCheckSum(chunkBytes);
            WriteUInt32(crc);
        }
    }
}
