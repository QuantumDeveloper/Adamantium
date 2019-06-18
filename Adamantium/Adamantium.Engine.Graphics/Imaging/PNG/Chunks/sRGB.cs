using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class sRGB: Chunk
    {
        public sRGB()
        {
            Name = "sRGB";
        }

        public RenderingIntent RenderingIntent { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.Add((byte)RenderingIntent);

            return bytes.ToArray();
        }

        internal static sRGB FromState(PNGState state)
        {
            return new sRGB() { RenderingIntent = state.InfoPng.SrgbIntent };
        }
    }
}
