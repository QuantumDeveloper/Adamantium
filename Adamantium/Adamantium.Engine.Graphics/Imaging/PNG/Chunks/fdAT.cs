using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class fdAT: Chunk
    {
        public fdAT()
        {
            Name = "fdAT";
        }

        public uint SequenceNumber { get; set; }

        public byte[] FrameData { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            throw new NotImplementedException();
        }
    }
}
