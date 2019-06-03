using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class tRNS : Chunk
    {
        public tRNS()
        {
            Name = "tRNS";
        }

        public uint KeyR { get; set; }

        public uint KeyG { get; set; }

        public uint KeyB { get; set; }
    }
}
