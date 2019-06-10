using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
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

        internal abstract byte[] GetChunkBytes(PNGState state);
    }
}
