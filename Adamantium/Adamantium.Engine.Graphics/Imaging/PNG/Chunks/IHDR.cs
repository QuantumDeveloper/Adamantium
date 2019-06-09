using Adamantium.Core;
using System;
using System.Collections.Generic;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class IHDR : Chunk
    {
        public IHDR()
        {
            Name = "IHDR";
        }

        public int Width { get; set; }

        public int Height { get; set; }

        public byte BitDepth { get; set; }

        public PNGColorType ColorType { get; set; }

        public byte CompressionMethod { get; set; }

        public byte FilterMethod { get; set; }

        public InterlaceMethod InterlaceMethod { get; set; }

        internal override byte[] GetChunkBytes(PNGColorMode info, PNGEncoderSettings settings)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Width));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Height));
            bytes.Add(BitDepth);
            bytes.Add((byte)ColorType);
            bytes.Add(CompressionMethod);
            bytes.Add(FilterMethod);
            bytes.Add((byte)InterlaceMethod);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }
    }
}
