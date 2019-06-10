using Adamantium.Engine.Graphics.Imaging.PNG.Chunks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
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

        internal void WriteSignature()
        {
            WriteBytes(PngHeader);
        }

        internal void WriteIHDR(PNGState state, int width, int height)
        {
            var bytes = IHDR.FromState(state, width, height).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WriteIDAT(PNGState state, byte[] rawBytes)
        {
            var bytes = IDAT.FromState(state, rawBytes).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WritebKGD(PNGState state)
        {
            var bytes = bKGD.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WritecHRM(PNGState state)
        {
            var bytes = cHRM.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WritegAMA(PNGState state)
        {
            var bytes = gAMA.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WriteiCCP(PNGState state)
        {
            var bytes = iCCP.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WriteIEND(PNGState state)
        {
            var bytes = new IEND().GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WriteiTXt(PNGState state)
        {
            var bytes = IHDR.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WritetEXt(PNGState state)
        {
            var bytes = IHDR.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WritezTXt(PNGState state)
        {
            var bytes = IHDR.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WritepHYs(PNGState state)
        {
            var bytes = pHYs.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WritePLTE(PNGState state)
        {
            var bytes = PLTE.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WritesRGB(PNGState state)
        {
            var bytes = sRGB.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WritetIME(PNGState state)
        {
            var bytes = tIME.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }

        internal void WritetRNS(PNGState state)
        {
            var bytes = tRNS.FromState(state).GetChunkBytes(state);
            WriteBytes(bytes);
        }
    }
}
