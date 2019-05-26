using Adamantium.Engine.Graphics.Imaging.PNG.Chunks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG
{
    public unsafe class PNGStream: UnmanagedMemoryStream
    {
        public PNGStream(IntPtr pSource, int size): base((byte*)pSource, size)
        {
        }
        public byte[] ReadBytes(int count)
        {
            var buffer = new byte[count];

            if (Read(buffer, 0, count) != count)
                throw new Exception("End reached.");

            return buffer;
        }

        public uint ErrorCode { get; private set; }

        public UInt16 ReadUInt16()
        {
            var bytes = ReadBytes(2);
            return BitConverter.ToUInt16(bytes.Reverse().ToArray(), 0);
        }

        public UInt32 ReadUInt32()
        {
            var bytes = ReadBytes(4);
            return BitConverter.ToUInt32(bytes.Reverse().ToArray(), 0);
        }

        public Int32 ReadInt32()
        {
            var bytes = ReadBytes(4);
            return BitConverter.ToInt32(bytes.Reverse().ToArray(), 0);
        }

        public uint ReadChunkSize()
        {
            return ReadUInt32();
        }

        public string ReadChunkType()
        {
            var bytes = ReadBytes(4);
            return Encoding.ASCII.GetString(bytes);
        }

        public IHDR ReadIHDR()
        {
            IHDR header = new IHDR();
            header.Width = ReadInt32();
            header.Height = ReadInt32();
            header.BitDepth = (byte)ReadByte();
            header.ColorType = (PNGColorType)ReadByte();
            header.CompressionMethod = (uint)ReadByte();
            header.FilterMethod = (uint)ReadByte();
            header.InterlaceMethod = (InterlaceMethod)ReadByte();
            header.CRC = ReadUInt32();
            Position = 12;
            var data = ReadBytes(17);
            header.CheckSum = Chunk.CalculateCheckSum(data, data.Length);

            return header;
        }

        public sRGB ReadsRGB()
        {
            var pos = Position-4;
            sRGB srgb = new sRGB();
            srgb.RenderingIntent = (RenderingIntent)ReadByte();
            srgb.CRC = ReadUInt32();
            Position = pos;
            var bytes = ReadBytes(5);
            srgb.CheckSum = Chunk.CalculateCheckSum(bytes, bytes.Length);
            return srgb;
        }

        public gAMA ReadgAMA()
        {
            var pos = Position - 4;
            gAMA gama = new gAMA();
            var data = ReadBytes(4);
            gama.Gamma = 16777216u * data[0] + 65536u * data[1] + 256u * data[2] + data[3];
            gama.CRC = ReadUInt32();
            Position = pos;
            var bytes = ReadBytes(5);
            gama.CheckSum = Chunk.CalculateCheckSum(bytes, bytes.Length);
            return gama;
        }

        public pHYs ReadpHYs()
        {
            var pos = Position - 4;
            pHYs phys = new pHYs();
            phys.PhysX = ReadUInt32();
            phys.PhysX = ReadUInt32();
            phys.Unit = (Unit)ReadByte();
            phys.CRC = ReadUInt32();
            Position = pos;
            var bytes = ReadBytes(13);
            phys.CheckSum = Chunk.CalculateCheckSum(bytes, bytes.Length);
            return phys;
        }

        internal tEXt ReadtEXtChunk(PNGState state, uint chunkLength)
        {
            var pos = Position - 4;
            var data = ReadBytes((int)chunkLength);
            var text = new tEXt();

            int length = 0;
            while (length < chunkLength && data[length] != 0) ++length;

            if (length < 1 || length > 79)
            {
                state.Error = 89;
            }

            text.Key = Encoding.ASCII.GetString(data, 0, length);
            length++;
            text.Text = Encoding.ASCII.GetString(data, length, data.Length - length);
            text.CRC = ReadUInt32();
            Position = pos;
            var bytes = ReadBytes(4+(int)chunkLength);
            text.CheckSum = Chunk.CalculateCheckSum(bytes, bytes.Length);

            return text;
        }
    }
}
