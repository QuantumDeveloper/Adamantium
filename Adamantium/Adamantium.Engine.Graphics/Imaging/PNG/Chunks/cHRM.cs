using Adamantium.Core;
using System.Collections.Generic;

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

        internal override byte[] GetChunkBytes(PNGState state)
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

            return bytes.ToArray();
        }

        internal static cHRM FromState(PNGState state)
        {
            var chrm = new cHRM();
            chrm.WhitePointX = state.InfoPng.ChrmWhiteX;
            chrm.WhitePointY = state.InfoPng.ChrmWhiteY;
            chrm.RedX = state.InfoPng.ChrmRedX;
            chrm.RedY = state.InfoPng.ChrmRedY;
            chrm.GreenX = state.InfoPng.ChrmGreenX;
            chrm.GreenY = state.InfoPng.ChrmGreenY;
            chrm.BlueX = state.InfoPng.ChrmBlueX;
            chrm.BlueY = state.InfoPng.ChrmBlueY;
            return chrm;
        }
    }
}
