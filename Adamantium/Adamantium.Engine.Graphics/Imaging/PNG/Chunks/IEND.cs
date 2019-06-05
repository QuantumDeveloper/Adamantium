﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class IEND : Chunk
    {
        public IEND()
        {
            Name = "IEND";
        }

        public override byte[] GetChunkBytes()
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());

            return bytes.ToArray();
        }
    }
}
