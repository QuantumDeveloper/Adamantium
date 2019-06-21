using Adamantium.Engine.Graphics.Imaging.PNG.Chunks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.IO
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

        internal void WriteIHDR(PNGState state, int width, int height)
        {
            var bytes = IHDR.FromState(state, width, height).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WriteIDAT(PNGState state, byte[] rawBytes)
        {
            var bytes = IDAT.FromState(state, rawBytes).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritebKGD(PNGState state)
        {
            var bytes = bKGD.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritecHRM(PNGState state)
        {
            var bytes = cHRM.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritegAMA(PNGState state)
        {
            var bytes = gAMA.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WriteiCCP(PNGState state)
        {
            var bytes = iCCP.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WriteIEND(PNGState state)
        {
            var bytes = new IEND().GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WriteiTXt(PNGState state)
        {
            foreach (var item in state.InfoPng.ITextItems)
            {
                var bytes = iTXt.FromTextItem(item).GetChunkBytes(state);
                WriteChunk(bytes);
            }
        }

        internal void WritetEXt(PNGState state)
        {
            foreach (var item in state.InfoPng.TextItems)
            {
                var bytes = tEXt.FromTextItem(item).GetChunkBytes(state);
                WriteChunk(bytes);
            }
        }

        internal void WritezTXt(PNGState state)
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

        internal void WritepHYs(PNGState state)
        {
            var bytes = pHYs.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritePLTE(PNGState state)
        {
            var bytes = PLTE.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritesRGB(PNGState state)
        {
            var bytes = sRGB.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritetIME(PNGState state)
        {
            var bytes = tIME.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritetRNS(PNGState state)
        {
            var bytes = tRNS.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        /*Animated PNG*/
        internal void WriteacTL(PNGState state)
        {
            var bytes = acTL.FromState(state).GetChunkBytes(state);
            WriteChunk(bytes);
        }

        internal void WritefcTL(PNGFrame frame)
        {
            var bytes = fcTL.FromFrame(frame).GetChunkBytes(null);
            WriteChunk(bytes);
        }

        internal void WritefdAT(byte[] rawData, uint sequenceNumber, PNGState state)
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
            var crc = CRC32.CalculateCheckSum(chunkBytes);
            WriteUInt32(crc);
        }
    }
}
