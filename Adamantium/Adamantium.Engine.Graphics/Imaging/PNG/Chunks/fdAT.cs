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
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(26u));
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(FrameData);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray()[4..]);
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }
    }
}
