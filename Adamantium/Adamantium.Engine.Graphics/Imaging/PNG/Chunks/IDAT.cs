﻿using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class IDAT : Chunk
    {
        public IDAT()
        {
            Name = "IDAT";
        }

        /// <summary>
        /// Raw color data in RGBARGBARGBA...
        /// </summary>
        public byte[] RawData { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(Utilities.GetBytesWithReversedEndian((uint)RawData.Length));
            bytes.AddRange(GetNameAsBytes());

            PNGCompressor compressor = new PNGCompressor();
            var compressedData = new List<byte>();
            var result = compressor.Compress(RawData, state.EncoderSettings, compressedData);
            if (result > 0)
            {
                throw new PNGEncoderException(result.ToString());
            }

            bytes.AddRange(compressedData);

            var crc = CRC32.CalculateCheckSum(compressedData.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }

        internal static IDAT FromState(PNGState state, byte[] rawData)
        {
            var data = new IDAT();
            data.RawData = rawData;
            return data;
        }
    }
}