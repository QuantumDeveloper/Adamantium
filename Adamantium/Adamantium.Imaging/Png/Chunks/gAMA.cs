﻿using System.Collections.Generic;
using Adamantium.Core;

namespace Adamantium.Imaging.Png.Chunks
{
    internal class gAMA: Chunk
    {
        public gAMA()
        {
            Name = "gAMA";
        }

        public uint Gamma { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Gamma));

            return bytes.ToArray();
        }

        internal static gAMA FromState(PNGState state)
        {
            return new gAMA() { Gamma = state.InfoPng.Gamma };
        }
    }
}