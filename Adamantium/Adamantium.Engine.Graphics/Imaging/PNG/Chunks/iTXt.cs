using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    /*international text chunk (iTXt)*/
    internal class iTXt : Chunk
    {
        public iTXt()
        {
            Name = "iTXt";
        }

        public string Key { get; set; }

        public string LangTag { get; set; }

        public string TransKey { get; set; }

        public string Text { get; set; }

        internal override byte[] GetChunkBytes(PNGColorMode info, PNGEncoderSettings settings)
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
            bytes.Add(settings.TextCompression ? (byte)1 : (byte)0); /*compression flag*/
            bytes.Add(0); // Compression method
            var langTagBytes = Encoding.ASCII.GetBytes(LangTag);
            bytes.AddRange(langTagBytes);
            bytes.Add(0); // Null terminator
            var transKeyBytes = Encoding.ASCII.GetBytes(TransKey);
            bytes.AddRange(transKeyBytes);
            bytes.Add(0); // Null terminator

            var textBytes = Encoding.ASCII.GetBytes(Text);
            if (settings.TextCompression)
            {
                var compressedData = new List<byte>();
                PNGCompressor compressor = new PNGCompressor();
                var error = compressor.Compress(textBytes, settings, compressedData);
                if (error > 0)
                {
                    throw new PNGEncoderException(error.ToString());
                }
                bytes.AddRange(compressedData);
            }
            else /*not compressed*/
            {
                bytes.AddRange(textBytes);
            }

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

            return bytes.ToArray();
        }
    }
}
