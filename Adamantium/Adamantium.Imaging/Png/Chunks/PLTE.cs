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

        internal override byte[] GetChunkBytes(PngState state)
        {
            var bytes = new List<byte>();

            bytes.AddRange(GetNameAsBytes());
            List<byte> palette = new List<byte>();
            for (int i = 0; i != state.ColorModeRaw.PaletteSize; ++i)
            {
                /*add all channels except alpha channel*/
                if (i % 4 != 3)
                {
                    palette.Add(state.ColorModeRaw.Palette[i]);
                }
            }
            bytes.AddRange(palette);

            return bytes.ToArray();
        }

        internal static PLTE FromState(PngState state)
        {
            var plte = new PLTE();
            plte.Palette = state.ColorModeRaw.Palette;
            return plte;
        }
    }
}
