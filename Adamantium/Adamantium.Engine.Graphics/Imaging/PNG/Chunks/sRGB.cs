using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class sRGB: Chunk
    {
        public sRGB()
        {
            Name = "sRGB";
        }

        public RenderingIntent RenderingIntent { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(1u));
            bytes.AddRange(GetNameAsBytes());
            bytes.Add((byte)RenderingIntent);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray()[4..]);
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }

        internal static sRGB FromState(PNGState state)
        {
            return new sRGB() { RenderingIntent = state.InfoPng.SrgbIntent };
        }
    }
}
