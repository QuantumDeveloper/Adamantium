using System.Text;

namespace Adamantium.Imaging.Png.Chunks
{
    internal abstract class Chunk
    {
        public string Name { get; set; }

        public uint CRC { get; set; }

        public uint CheckSum { get; internal set; }

        public byte[] GetNameAsBytes()
        {
            return Encoding.ASCII.GetBytes(Name);
        }

        internal abstract byte[] GetChunkBytes(PngState state);
    }
}
