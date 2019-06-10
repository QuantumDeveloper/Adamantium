using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    // Compressed textual data
    internal class zTXt : Chunk
    {
        public zTXt()
        {
            Name = "zTXt";
        }

        public string Key { get; set; }

        public string Text { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(GetNameAsBytes());
            var keyWord = Encoding.ASCII.GetBytes(Key);
            if (keyWord.Length < 1 || keyWord.Length > 79)
            {
                throw new PNGEncoderException("Keyword should have from 1 to 79 chars");
            }

            bytes.AddRange(keyWord);
            bytes.Add(0); // Null terminator
            bytes.Add(0); // Compression method

            PNGCompressor compressor = new PNGCompressor();
            var textBytes = Encoding.ASCII.GetBytes(Text);
            var compressedText = new List<byte>();
            var result = compressor.Compress(textBytes, state.EncoderSettings, compressedText);
            if (result > 0)
            {
                throw new PNGEncoderException(result.ToString());
            }
            bytes.AddRange(compressedText);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }
    }
}
