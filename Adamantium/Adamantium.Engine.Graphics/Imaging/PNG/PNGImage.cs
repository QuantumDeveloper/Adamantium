using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    internal class PNGImage
    {
        public int Width { get; set; }

        public int Height { get; set; }

        public int BitDepth { get; set; }



        public PNGImage(PNGColorMode mode)
        {
            Frames = new List<PNGFrame>();
        }

        public List<PNGFrame> Frames { get; }
    }
}
