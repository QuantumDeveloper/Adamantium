using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class gAMA: Chunk
    {
        public gAMA()
        {
            Name = "gAMA";
        }
        public uint Gamma { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(4u));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Gamma));

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }

        internal static gAMA FromState(PNGState state)
        {
            return new gAMA() { Gamma = state.InfoPng.Gamma };
        }
    }
}
