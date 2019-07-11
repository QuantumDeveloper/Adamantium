using System.Collections.Generic;

namespace Adamantium.Imaging.Png.Chunks
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
            bytes.AddRange(GetNameAsBytes());
            if (state.InfoRaw.ColorType == PNGColorType.Grey || state.InfoRaw.ColorType == PNGColorType.GreyAlpha)
            {
                bytes.Add((byte)(BackgroundR >> 8));
                bytes.Add((byte)(BackgroundR & 255));
            }
            else if (state.InfoRaw.ColorType == PNGColorType.RGB || state.InfoRaw.ColorType == PNGColorType.RGBA)
            {
                bytes.Add((byte)(BackgroundR >> 8));
                bytes.Add((byte)(BackgroundR & 255));
                bytes.Add((byte)(BackgroundG >> 8));
                bytes.Add((byte)(BackgroundG & 255));
                bytes.Add((byte)(BackgroundB >> 8));
                bytes.Add((byte)(BackgroundB & 255));
            }
            else if (state.InfoRaw.ColorType == PNGColorType.Palette)
            {
                bytes.Add((byte)(BackgroundR & 255)); /*palette index*/
            }

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
