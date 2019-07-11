using System.Collections.Generic;
using Adamantium.Core;

namespace Adamantium.Imaging.Png.Chunks
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
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(FramesCount));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(RepeatCout));

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
