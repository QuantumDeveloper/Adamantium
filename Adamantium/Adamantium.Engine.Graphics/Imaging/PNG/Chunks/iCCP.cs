using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    public class iCCP : Chunk
    {
        public iCCP()
        {
            Name = "iCCP";
        }

        public string ICCPName { get; set; }

        public byte[] Profile { get; set; }
    }
}
