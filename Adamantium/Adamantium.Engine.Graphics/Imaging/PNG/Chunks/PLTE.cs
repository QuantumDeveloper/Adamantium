using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class PLTE : Chunk
    {
        public PLTE()
        {
            Name = "PLTE";
        }

        public int PaletteSize { get; set; }

        public byte[] Palette { get; set; }

        internal override byte[] GetChunkBytes(PNGColorMode info, PNGEncoderSettings settings)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            List<byte> palette = new List<byte>();
            for (int i = 0; i != info.PaletteSize; ++i)
            {
                /*add all channels except alpha channel*/
                if (i % 4 != 3)
                {
                    palette.Add(info.Palette[i]);
                }
            }
            bytes.AddRange(palette);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }
    }
}
