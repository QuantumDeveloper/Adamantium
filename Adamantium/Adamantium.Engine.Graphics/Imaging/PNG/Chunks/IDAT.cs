using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class IDAT : Chunk
    {
        public IDAT()
        {
            Name = "IDAT";
        }

        public byte[] RawData { get; set; }

        public override byte[] GetChunkBytes()
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());


            return bytes.ToArray();
        }
    }
}
