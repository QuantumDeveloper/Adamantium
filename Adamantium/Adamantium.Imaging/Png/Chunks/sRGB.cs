using System.Collections.Generic;

namespace Adamantium.Imaging.Png.Chunks
{
    internal class sRGB: Chunk
    {
        public sRGB()
        {
            Name = "sRGB";
        }

        public RenderingIntent RenderingIntent { get; set; }

        internal override byte[] GetChunkBytes(PngState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.Add((byte)RenderingIntent);

            return bytes.ToArray();
        }

        internal static sRGB FromState(PngState state)
        {
            return new sRGB() { RenderingIntent = state.InfoPng.SrgbIntent };
        }
    }
}
