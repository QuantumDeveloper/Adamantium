using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    //Primary chromaticities chunk
    internal class cHRM : Chunk
    {
        public cHRM()
        {
            Name = "cHRM";
        }

        public uint WhitePointX { get; set; }
        public uint WhitePointY { get; set; }
        public uint RedX { get; set; }
        public uint RedY { get; set; }
        public uint GreenX { get; set; }
        public uint GreenY { get; set; }
        public uint BlueX { get; set; }
        public uint BlueY { get; set; }
    }
}
