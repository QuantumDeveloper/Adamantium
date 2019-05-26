using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    public class sRGB: Chunk
    {
        public sRGB()
        {
            Name = "sRGB";
        }

        public RenderingIntent RenderingIntent { get; set; }
    }
}
