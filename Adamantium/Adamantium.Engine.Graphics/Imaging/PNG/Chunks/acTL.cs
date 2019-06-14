using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class acTL: Chunk
    {
        public acTL()
        {
            Name = "acTL";
        }

        public uint FramesCount { get; set; }

        public uint RepeatCout { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            return null;
        }
    }
}
