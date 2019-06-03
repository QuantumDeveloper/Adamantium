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
    }
}
