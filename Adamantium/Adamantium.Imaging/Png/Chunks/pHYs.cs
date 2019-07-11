using System.Collections.Generic;
using Adamantium.Core;

namespace Adamantium.Imaging.Png.Chunks
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
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(PhysX));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(PhysY));
            bytes.Add((byte)Unit);

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