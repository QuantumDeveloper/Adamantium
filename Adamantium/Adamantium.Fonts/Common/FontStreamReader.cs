﻿using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Adamantium.Fonts.Common
{
    internal class FontStreamReader : MemoryStream
    {
        public string FilePath { get; }
        
        public FontStreamReader()
        {
        }
        
        public FontStreamReader(byte[] buffer, string path = "") : base(buffer)
        {
            FilePath = path;
        }
        
        public byte[] ReadBytes(long count, bool ignoreEndian = false)
        {
            byte[] bytes = new byte[count];
            for (int i = 0; i < count; ++i)
            {
                bytes[i] = ReadByte();
            }

            if (BitConverter.IsLittleEndian && !ignoreEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }
        
        public sbyte[] ReadSignedBytes(long count)
        {
            sbyte[] bytes = new sbyte[count];
            for (int i = 0; i < count; ++i)
            {
                bytes[i] = ReadSignedByte();
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }

        public sbyte ReadSignedByte()
        {
            return (sbyte)base.ReadByte();
        }

        public new byte ReadByte()
        {
            return (byte)base.ReadByte();
        }

        public UInt16 ReadUInt16()
        {
            var bytes = ReadBytes(Marshal.SizeOf<UInt16>());
            return BitConverter.ToUInt16(bytes, 0);
        }

        public UInt32 ReadUInt24()
        {
            var bytes = new byte[4];
            bytes[2] = ReadByte();
            bytes[1] = ReadByte();
            bytes[0] = ReadByte();
            return BitConverter.ToUInt32(bytes.ToArray(), 0);
        }

        public UInt32 ReadUInt32()
        {
            var bytes = ReadBytes(Marshal.SizeOf<UInt32>());
            return BitConverter.ToUInt32(bytes, 0);
        }

        public UInt64 ReadUInt64()
        {
            var bytes = ReadBytes(Marshal.SizeOf<UInt64>());
            return BitConverter.ToUInt64(bytes, 0);
        }

        public Int16 ReadInt16()
        {
            var bytes = ReadBytes(Marshal.SizeOf<Int16>());
            return BitConverter.ToInt16(bytes, 0);
        }

        public Int32 ReadInt32()
        {
            var bytes = ReadBytes(Marshal.SizeOf<Int32>());
            return BitConverter.ToInt32(bytes, 0);
        }

        public Int64 ReadInt64()
        {
            var bytes = ReadBytes(Marshal.SizeOf<Int64>());
            return BitConverter.ToInt64(bytes, 0);
        }

        public Single ReadFloat()
        {
            var bytes = ReadBytes(Marshal.SizeOf<Single>());
            return BitConverter.ToSingle(bytes, 0);
        }

        public String ReadString(int length)
        {
            var bytes = ReadBytes(length);
            return Encoding.ASCII.GetString(bytes.Reverse().ToArray(), 0, bytes.Length);
        }
        
        public String ReadString(int length, Encoding encoding)
        {
            var bytes = ReadBytes(length);
            return encoding.GetString(bytes.Reverse().ToArray(), 0, bytes.Length);
        }
    }
}
