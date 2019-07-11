using System.Collections.Generic;

namespace Adamantium.Imaging.Png.Chunks
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
