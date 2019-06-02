using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    /*international text chunk (iTXt)*/
    internal class iTXt : Chunk
    {
        public iTXt()
        {
            Name = "iTXt";
        }

        public string Key { get; set; }

        public string LangTag { get; set; }

        public string TransKey { get; set; }

        public string Text { get; set; }
    }
}
