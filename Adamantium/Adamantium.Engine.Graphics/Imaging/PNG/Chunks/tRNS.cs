using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class tRNS : Chunk
    {
        public tRNS()
        {
            Name = "tRNS";
        }

        public uint KeyR { get; set; }

        public uint KeyG { get; set; }

        public uint KeyB { get; set; }

        public override byte[] GetChunkBytes()
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Year));
            bytes.Add(Month);
            bytes.Add(Day);
            bytes.Add(Hour);
            bytes.Add(Minute);
            bytes.Add(Second);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));
        }
    }
}
