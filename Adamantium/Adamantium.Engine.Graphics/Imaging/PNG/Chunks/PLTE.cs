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

        public int PaletteSize => Palette == null ? 0 : Palette.Length;

        public byte[] Palette { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Palette.Length));
            bytes.AddRange(GetNameAsBytes());
            List<byte> palette = new List<byte>();
            for (int i = 0; i != state.InfoRaw.PaletteSize; ++i)
            {
                /*add all channels except alpha channel*/
                if (i % 4 != 3)
                {
                    palette.Add(state.InfoRaw.Palette[i]);
                }
            }
            bytes.AddRange(palette);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }

        internal static PLTE FromState(PNGState state)
        {
            var plte = new PLTE();
            plte.Palette = state.InfoRaw.Palette;
            return plte;
        }
    }
}
