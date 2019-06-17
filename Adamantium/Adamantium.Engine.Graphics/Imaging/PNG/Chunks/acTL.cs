using Adamantium.Core;
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
            var bytes = new List<byte>();
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(32u));
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(FramesCount));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(RepeatCout));

            var crc = CRC32.CalculateCheckSum(bytes.ToArray()[4..]);
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }

        internal static acTL FromState(PNGState state)
        {
            var actl = new acTL();
            actl.FramesCount = state.InfoPng.FramesCount;
            actl.RepeatCout = state.InfoPng.RepeatCount;

            return actl;
        }
    }
}
