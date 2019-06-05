using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    public abstract class Chunk
    {
        public string Name { get; set; }

        public uint CRC { get; set; }

        public uint CheckSum { get; internal set; }

        public byte[] GetNameAsBytes()
        {
            return Encoding.ASCII.GetBytes(Name);
        }

        public abstract byte[] GetChunkBytes();
    }
}
