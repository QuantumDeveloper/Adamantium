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

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            var bkgd = new List<byte>();
            if (state.InfoRaw.ColorType == PNGColorType.Grey || state.InfoRaw.ColorType == PNGColorType.GreyAlpha)
            {
                bytes.AddRange(Utilities.GetBytesWithReversedEndian(2u));
                bkgd.Add((byte)(BackgroundR >> 8));
                bkgd.Add((byte)(BackgroundR & 255));
            }
            else if (state.InfoRaw.ColorType == PNGColorType.RGB || state.InfoRaw.ColorType == PNGColorType.RGBA)
            {
                bytes.AddRange(Utilities.GetBytesWithReversedEndian(6u));
                bkgd.Add((byte)(BackgroundR >> 8));
                bkgd.Add((byte)(BackgroundR & 255));
                bkgd.Add((byte)(BackgroundG >> 8));
                bkgd.Add((byte)(BackgroundG & 255));
                bkgd.Add((byte)(BackgroundB >> 8));
                bkgd.Add((byte)(BackgroundB & 255));
            }
            else if (state.InfoRaw.ColorType == PNGColorType.Palette)
            {
                bytes.AddRange(Utilities.GetBytesWithReversedEndian(1u));
                bkgd.Add((byte)(BackgroundR & 255)); /*palette index*/
            }

            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(bkgd);
            var crc = CRC32.CalculateCheckSum(bytes.ToArray()[4..]);
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }

        internal static bKGD FromState(PNGState state)
        {
            var bkgd = new bKGD();
            bkgd.BackgroundR = state.InfoPng.BackgroundR;
            bkgd.BackgroundG = state.InfoPng.BackgroundG;
            bkgd.BackgroundB = state.InfoPng.BackgroundB;
            return bkgd;
        }
    }
}
