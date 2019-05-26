using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    public class gAMA: Chunk
    {
        public gAMA()
        {
            Name = "gAMA";
        }
        public uint Gamma { get; set; }
    }
}
