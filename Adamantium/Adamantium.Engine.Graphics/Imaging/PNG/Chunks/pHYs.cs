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

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(9u));

            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(PhysX));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(PhysY));
            bytes.Add((byte)Unit);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray()[4..]);
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }

        internal static pHYs FromState(PNGState state)
        {
            var phys = new pHYs();
            phys.PhysX = state.InfoPng.PhysX;
            phys.PhysY = state.InfoPng.PhysY;
            phys.Unit = state.InfoPng.PhysUnit;
            return phys;
        }
    }
}