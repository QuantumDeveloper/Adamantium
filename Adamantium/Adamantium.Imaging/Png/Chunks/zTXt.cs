using System.Collections.Generic;
using System.Text;

namespace Adamantium.Imaging.Png.Chunks
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

        internal override byte[] GetChunkBytes(PngState state)
        {
            var bytes = new List<byte>();
            var compressedBytes = new List<byte>();

            
            var keyWord = Encoding.ASCII.GetBytes(Key);
            if (keyWord.Length < 1 || keyWord.Length > 79)
            {
                throw new PngEncoderException(89);
            }

            compressedBytes.AddRange(keyWord);
            compressedBytes.Add(0); // Null terminator
            compressedBytes.Add(0); // Compression method

            PngCompressor compressor = new PngCompressor();
            var textBytes = Encoding.ASCII.GetBytes(Text);
            var compressedText = new List<byte>();
            var result = compressor.Compress(textBytes, state.EncoderSettings, compressedText);
            if (result > 0)
            {
                throw new PngEncoderException(result);
            }
            compressedBytes.AddRange(compressedText);

            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(compressedBytes);

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
