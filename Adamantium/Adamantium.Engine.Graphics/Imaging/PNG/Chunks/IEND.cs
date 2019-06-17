using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class IEND : Chunk
    {
        public IEND()
        {
            Name = "IEND";
        }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(0U));
            bytes.AddRange(GetNameAsBytes());
            var crc = CRC32.CalculateCheckSum(GetNameAsBytes()[4..]);
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));
            return bytes.ToArray();
        }
    }
}
