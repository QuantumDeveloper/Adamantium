using Adamantium.Core;
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
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(SequenceNumber));
            bytes.AddRange(FrameData);

            return bytes.ToArray();
        }
    }
}
