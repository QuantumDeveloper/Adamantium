using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    //Primary chromaticities chunk
    internal class cHRM : Chunk
    {
        public cHRM()
        {
            Name = "cHRM";
        }

        public uint WhitePointX { get; set; }
        public uint WhitePointY { get; set; }
        public uint RedX { get; set; }
        public uint RedY { get; set; }
        public uint GreenX { get; set; }
        public uint GreenY { get; set; }
        public uint BlueX { get; set; }
        public uint BlueY { get; set; }

        internal override byte[] GetChunkBytes(PNGColorMode info, PNGEncoderSettings settings)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(WhitePointX));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(WhitePointY));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(RedX));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(RedY));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(GreenX));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(GreenY));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(BlueX));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(BlueY));

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }
    }
}
