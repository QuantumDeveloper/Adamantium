using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    public abstract class Chunk
    {
        public string Name { get; set; }

        public uint CRC { get; set; }

        public uint CheckSum { get; internal set; }

        public static uint CalculateCheckSum(byte[] data)
        {
            uint r = 0xffffffffu;
            for (int i = 0; i < data.Length; ++i)
            {
                r = CRC32.CRC32Table[(r ^ data[i]) & 0xff] ^ (r >> 8);
            }
            return r ^ 0xffffffffu;
        }

        public byte[] GetNameAsBytes()
        {
            return Encoding.ASCII.GetBytes(Name);
        }

        public abstract byte[] GetChunkBytes();
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

        public byte CompressionMethod { get; set; }

        public byte FilterMethod { get; set; }

        public InterlaceMethod InterlaceMethod { get; set; }

        public override byte[] GetChunkBytes()
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Width));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Height));
            bytes.Add(BitDepth);
            bytes.Add((byte)ColorType);
            bytes.Add(CompressionMethod);
            bytes.Add(FilterMethod);
            bytes.Add((byte)InterlaceMethod);

            return bytes.ToArray();
        }
    }
}
