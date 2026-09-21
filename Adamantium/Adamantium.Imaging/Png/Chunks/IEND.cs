using System.Collections.Generic;

namespace Adamantium.Imaging.Png.Chunks
{
    internal class IEND : Chunk
    {
        public IEND()
        {
            Name = "IEND";
        }

        internal override byte[] GetChunkBytes(PngState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            return bytes.ToArray();
        }
    }
}
