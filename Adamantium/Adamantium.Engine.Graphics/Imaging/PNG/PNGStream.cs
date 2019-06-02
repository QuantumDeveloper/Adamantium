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

        internal tEXt ReadtEXt(PNGState state, uint chunkLength)
        {
            var pos = Position - 4;
            var data = ReadBytes((int)chunkLength);
            var text = new tEXt();

            int length = 0;
            while (length < chunkLength && data[length] != 0) ++length;

            if (length < 1 || length > 79)
            {
                /*keyword too short or long*/
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

        internal iCCP ReadiCCP(PNGState state, PNGDecoder decoder, uint chunkLength)
        {
            var pos = Position - 4;
            var data = ReadBytes((int)chunkLength);
            var iccp = new iCCP();

            int length = 0;
            while (length < chunkLength && data[length] != 0) ++length;
            if (length + 2 >= chunkLength)
            {
                /*no null termination, corrupt?*/
                state.Error = 75;
            }

            if (length < 1 || length > 79)
            {
                /*keyword too short or long*/
                state.Error = 89;
            }

            iccp.ICCPName = Encoding.ASCII.GetString(data, 0, length);
            length++;
            if (data[length] != 0)
            {
                /*the 0 byte indicating compression must be 0*/
                state.Error = 72;
            }
            length++;

            var slice = data[length..];
            List<byte> decoded = new List<byte>();
            state.Error = decoder.Decompress(slice, state.DecoderSettings, decoded);
            if (state.Error == 0)
            {
                iccp.Profile = decoded.ToArray();
            }

            iccp.CRC = ReadUInt32();
            Position = pos;
            var bytes = ReadBytes(4 + (int)chunkLength);
            iccp.CheckSum = Chunk.CalculateCheckSum(bytes, bytes.Length);

            return iccp;
        }

        internal cHRM ReadcHRM()
        {
            var pos = Position - 4;
            var chrm = new cHRM();
            chrm.WhitePointX = ReadUInt32();
            chrm.WhitePointY = ReadUInt32();
            chrm.RedX = ReadUInt32();
            chrm.RedY = ReadUInt32();
            chrm.GreenX = ReadUInt32();
            chrm.GreenY = ReadUInt32();
            chrm.BlueX = ReadUInt32();
            chrm.BlueY = ReadUInt32();

            chrm.CRC = ReadUInt32();
            Position = pos;
            var bytes = ReadBytes(36);
            chrm.CheckSum = Chunk.CalculateCheckSum(bytes, bytes.Length);
            return chrm;
        }

        internal iTXt ReadiTXt(PNGState state, PNGDecoder decoder, uint chunkLength)
        {
            if (chunkLength < 5)
            {
                /*iTXt chunk too short*/
                state.Error = 5;
                return null;
            }

            var pos = Position - 4;
            var data = ReadBytes((int)chunkLength);
            var itxt = new iTXt();
            byte compressed;

            int length = 0;
            while (length < chunkLength && data[length] != 0) ++length;
            if (length + 2 >= chunkLength)
            {
                /*no null termination, corrupt?*/
                state.Error = 75;
            }

            if (length < 1 || length > 79)
            {
                /*keyword too short or long*/
                state.Error = 89;
            }

            itxt.Key = Encoding.ASCII.GetString(data, 0, length);
            length++;
            compressed = data[length];
            if (data[length+1] != 0)
            {
                /*the 0 byte indicating compression must be 0*/
                state.Error = 72;
            }

            /*even though it's not allowed by the standard, no error is thrown if
            there's no null termination char, if the text is empty for the next 3 texts*/
            length+=1;
            var begin = length;
            length = 0;
            for (int i = begin; i < chunkLength && data[i] != 0; ++i) ++length;

            var langTag = data[begin..begin + length];
            itxt.LangTag = Encoding.ASCII.GetString(langTag);

            begin += length + 1;
            length = 0;
            for (int i = begin; i < chunkLength && data[i] != 0; ++i) ++length;
            var transKey = data[begin..begin + length];
            itxt.TransKey = Encoding.ASCII.GetString(transKey);

            if (length == 0)
            {
                length++;
            }

            begin += length + 1;

            var slice = data[begin..];
            List<byte> decoded = new List<byte>();
            if (compressed == 1)
            {
                state.Error = decoder.Decompress(slice, state.DecoderSettings, decoded);
                if (state.Error > 0)
                {
                    return null;
                }
            }
            else
            {
                decoded.AddRange(slice);
            }

            itxt.Text = Encoding.ASCII.GetString(decoded.ToArray());
            itxt.CRC = ReadUInt32();
            Position = pos;
            var bytes = ReadBytes(4+(int)chunkLength);
            itxt.CheckSum = Chunk.CalculateCheckSum(bytes, bytes.Length);

            state.InfoPng.InternationalText = itxt;
            return itxt;
        }

        internal zTXt ReadzTXt(PNGState state, PNGDecoder decoder, uint chunkLength)
        {
            var pos = Position - 4;
            var data = ReadBytes((int)chunkLength);
            var ztxt = new zTXt();

            int length = 0;
            while (length < chunkLength && data[length] != 0) ++length;
            if (length + 2 >= chunkLength)
            {
                /*no null termination, corrupt?*/
                state.Error = 75;
            }

            if (length < 1 || length > 79)
            {
                /*keyword too short or long*/
                state.Error = 89;
            }

            ztxt.Key = Encoding.ASCII.GetString(data, 0, length);
            length++;
            if (data[length] != 0)
            {
                /*the 0 byte indicating compression must be 0*/
                state.Error = 72;
                return null;
            }

            var begin = length + 1;
            if (begin > chunkLength)
            {
                /*no null termination, corrupt?*/
                state.Error = 75;
                return null;
            }

            List<byte> decoded = new List<byte>();
            var slice = data[begin..];
            state.Error = decoder.Decompress(slice, state.DecoderSettings, decoded);

            if (state.Error > 0)
            {
                return null;
            }
            ztxt.Text = Encoding.ASCII.GetString(decoded.ToArray());

            ztxt.CRC = ReadUInt32();
            Position = pos;
            var bytes = ReadBytes(4 + (int)chunkLength);
            ztxt.CheckSum = Chunk.CalculateCheckSum(bytes, bytes.Length);

            return ztxt;
        }

        internal tIME ReadtIME(PNGState state)
        {
            var pos = Position - 4;
            tIME time = new tIME();
            time.Year = ReadUInt16();
            time.Month = (byte)ReadByte();
            time.Day = (byte)ReadByte();
            time.Hour = (byte)ReadByte();
            time.Minute = (byte)ReadByte();
            time.Second = (byte)ReadByte();

            time.CRC = ReadUInt32();
            Position = pos;
            var bytes = ReadBytes(11);
            time.CheckSum = Chunk.CalculateCheckSum(bytes, bytes.Length);

            state.InfoPng.Time = time;

            return time;
        }
    }
}
