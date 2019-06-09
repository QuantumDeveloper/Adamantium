using Adamantium.Core;
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

        public ushort KeyR { get; set; }

        public ushort KeyG { get; set; }

        public ushort KeyB { get; set; }

        internal override byte[] GetChunkBytes(PNGColorMode info, PNGEncoderSettings settings)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            
            if (info.ColorType == PNGColorType.Palette)
            {
                var amount = info.PaletteSize;
                /*the tail of palette values that all have 255 as alpha, does not have to be encoded*/
                for (long i = info.PaletteSize; i != 0; --i)
                {
                    if (info.Palette[4 * (i - 1) + 3] == 255)
                    {
                        --amount;
                    }
                    else break;
                }
                /*add only alpha channel*/
                for (int i = 0; i != amount; ++i)
                {
                    bytes.Add(info.Palette[4 * i + 3]);
                }
            }
            else if (info.ColorType == PNGColorType.Grey)
            {
                bytes.Add((byte)(KeyR >> 8));
                bytes.Add((byte)(KeyR & 255));
            }
            else if (info.ColorType == PNGColorType.RGB)
            {
                bytes.Add((byte)(KeyR >> 8));
                bytes.Add((byte)(KeyR & 255));
                bytes.Add((byte)(KeyG >> 8));
                bytes.Add((byte)(KeyG & 255));
                bytes.Add((byte)(KeyB >> 8));
                bytes.Add((byte)(KeyB & 255));
            }

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }
    }
}
