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
            var compressedBytes = new List<byte>();

            
            var keyWord = Encoding.ASCII.GetBytes(Key);
            if (keyWord.Length < 1 || keyWord.Length > 79)
            {
                throw new PNGEncoderException(89);
            }

            compressedBytes.AddRange(keyWord);
            compressedBytes.Add(0); // Null terminator
            compressedBytes.Add(0); // Compression method

            PNGCompressor compressor = new PNGCompressor();
            var textBytes = Encoding.ASCII.GetBytes(Text);
            var compressedText = new List<byte>();
            var result = compressor.Compress(textBytes, state.EncoderSettings, compressedText);
            if (result > 0)
            {
                throw new PNGEncoderException(result);
            }
            compressedBytes.AddRange(compressedText);

            bytes.AddRange(Utilities.GetBytesWithReversedEndian(compressedBytes.Count));
            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(compressedBytes);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }

        internal static zTXt FromTextItem(TXTItem item)
        {
            var ztext = new zTXt();
            ztext.Key = item.Key;
            ztext.Text = item.Text;

            return ztext;
        }
    }
}
