using System.Collections.Generic;
using Adamantium.Core;

namespace Adamantium.Imaging.Png.Chunks
{
    internal class fdAT: Chunk
    {
        public fdAT()
        {
            Name = "fdAT";
        }

        public uint SequenceNumber { get; set; }

        public byte[] FrameData { get; set; }

        internal override byte[] GetChunkBytes(PngState state)
        {
            PngCompressor compressor = new PngCompressor();
            var compressedData = new List<byte>();
            var result = compressor.Compress(FrameData, state.EncoderSettings, compressedData);
            if (result > 0)
            {
                throw new PngEncoderException(result);
            }

            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(SequenceNumber));
            bytes.AddRange(compressedData);

            return bytes.ToArray();
        }
    }
}
