using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class bKGD : Chunk
    {
        public bKGD()
        {
            Name = "bKGD";
        }

        public uint BackgroundR { get; set; }

        public uint BackgroundG { get; set; }

        public uint BackgroundB { get; set; }
    }
}
