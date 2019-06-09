using Adamantium.Core;
using System.Collections.Generic;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class pHYs: Chunk
    {
        public pHYs()
        {
            Name = "pHYs";
        }

        public uint PhysX { get; set; }
        public uint PhysY { get; set; }
        public Unit Unit { get; set; }

        internal override byte[] GetChunkBytes(PNGColorMode info, PNGEncoderSettings settings)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(PhysX));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(PhysY));
            bytes.Add((byte)Unit);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }
    }
}