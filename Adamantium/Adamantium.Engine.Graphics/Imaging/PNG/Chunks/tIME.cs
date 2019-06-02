using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class tIME : Chunk
    {
        public ushort Year { get; set; }
        public byte Month { get; set; }
        public byte Day { get; set; }
        public byte Hour { get; set; }
        public byte Minute { get; set; }
        public byte Second { get; set; }
    }
}
