using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    public class tEXt : Chunk
    {
        public string Key { get; set; }

        public string Text { get; set; }
    }
}
