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
            PNGCompressor compressor = new PNGCompressor();
            var compressedData = new List<byte>();
            var result = compressor.Compress(FrameData, state.EncoderSettings, compressedData);
            if (result > 0)
            {
                throw new PNGEncoderException(result);
            }

            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(SequenceNumber));
            bytes.AddRange(compressedData);

            return bytes.ToArray();
        }
    }
}
