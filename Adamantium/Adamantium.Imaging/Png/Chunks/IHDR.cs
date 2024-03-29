﻿using System.Collections.Generic;
using Adamantium.Core;

namespace Adamantium.Imaging.Png.Chunks
{
    internal class IHDR : Chunk
    {
        public IHDR()
        {
            Name = "IHDR";
        }

        public int Width { get; set; }

        public int Height { get; set; }

        public byte BitDepth { get; set; }

        public PNGColorType ColorType { get; set; }

        public byte CompressionMethod { get; set; }

        public byte FilterMethod { get; set; }

        public InterlaceMethod InterlaceMethod { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Width));
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Height));
            bytes.Add(BitDepth);
            bytes.Add((byte)ColorType);
            bytes.Add(CompressionMethod);
            bytes.Add(FilterMethod);
            bytes.Add((byte)InterlaceMethod);

            return bytes.ToArray();
        }

        internal static IHDR FromState(PNGState state, int width, int height)
        {
            IHDR header = new IHDR();
            header.Width = width;
            header.Height = height;
            header.BitDepth = (byte)state.InfoPng.ColorMode.BitDepth;
            header.ColorType = state.InfoPng.ColorMode.ColorType;
            header.CompressionMethod = state.InfoPng.CompressionMethod;
            header.FilterMethod = state.InfoPng.FilterMethod;
            header.InterlaceMethod = state.InfoPng.InterlaceMethod;
            return header;
        }
    }
}
