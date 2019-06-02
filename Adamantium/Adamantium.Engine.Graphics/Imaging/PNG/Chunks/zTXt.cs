using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    // Compressed textual data
    internal class zTXt : Chunk
    {
        public zTXt()
        {
            Name = "zTXt";
        }

        public string Key { get; set; }

        public string Text { get; set; }
    }
}
