using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class bKGD : Chunk
    {
        public bKGD()
        {
            Name = "bKGD";
        }

        public uint BackgroundR { get; set; }

        public uint BackgroundG { get; set; }

        public uint BackgroundB { get; set; }

        internal override byte[] GetChunkBytes(PNGColorMode info, PNGEncoderSettings settings)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            var bkgd = new List<byte>();
            if (info.ColorType == PNGColorType.Grey || info.ColorType == PNGColorType.GreyAlpha)
            {
                bkgd.Add((byte)(BackgroundR >> 8));
                bkgd.Add((byte)(BackgroundR & 255));
            }
            else if (info.ColorType == PNGColorType.RGB || info.ColorType == PNGColorType.RGBA)
            {
                bkgd.Add((byte)(BackgroundR >> 8));
                bkgd.Add((byte)(BackgroundR & 255));
                bkgd.Add((byte)(BackgroundG >> 8));
                bkgd.Add((byte)(BackgroundG & 255));
                bkgd.Add((byte)(BackgroundB >> 8));
                bkgd.Add((byte)(BackgroundB & 255));
            }
            else if (info.ColorType == PNGColorType.Palette)
            {
                bkgd.Add((byte)(BackgroundR & 255)); /*palette index*/
            }

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }
    }
}
