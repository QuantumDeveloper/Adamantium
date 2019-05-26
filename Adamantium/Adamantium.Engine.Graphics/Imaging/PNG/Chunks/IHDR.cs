using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    public class Chunk
    {
        public string Name { get; set; }

        public uint CRC { get; set; }

        public uint CheckSum { get; internal set; }

        public static uint CalculateCheckSum(byte[] data, int length)
        {
            uint r = 0xffffffffu;
            for (int i = 0; i < length; ++i)
            {
                r = PNGHelper.CRC32Table[(r ^ data[i]) & 0xff] ^ (r >> 8);
            }
            return r ^ 0xffffffffu;
        }

    }

    public class IHDR : Chunk
    {
        public IHDR()
        {
            Name = "IHDR";
        }

        public int Width { get; set; }

        public int Height { get; set; }

        public byte BitDepth { get; set; }

        public PNGColorType ColorType { get; set; }

        public uint CompressionMethod { get; set; }

        public uint FilterMethod { get; set; }

        public InterlaceMethod InterlaceMethod { get; set; }
    }
}
