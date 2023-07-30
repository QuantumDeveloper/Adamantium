using System.Collections.Generic;
using System.Text;

namespace Adamantium.Imaging.Png.Chunks
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

        internal override byte[] GetChunkBytes(PngState state)
        {
            var bytes = new List<byte>();

            var keyWord = Encoding.ASCII.GetBytes(Key);
            if (keyWord.Length < 1 || keyWord.Length > 79)
            {
                throw new PngEncoderException(89);
            }
            var langTagBytes = Encoding.ASCII.GetBytes(LangTag);
            var transKeyBytes = Encoding.ASCII.GetBytes(TransKey);

            var textBytes = Encoding.ASCII.GetBytes(Text);

            var compressedData = new List<byte>();
            if (state.EncoderSettings.TextCompression)
            {
                PngCompressor compressor = new PngCompressor();
                var error = compressor.Compress(textBytes, state.EncoderSettings, compressedData);
                if (error > 0)
                {
                    throw new PngEncoderException(error);
                }
            }

            bytes.AddRange(GetNameAsBytes());
            bytes.AddRange(keyWord);
            bytes.Add(0); // Null terminator
            bytes.Add(state.EncoderSettings.TextCompression ? (byte)1 : (byte)0); /*compression flag*/
            bytes.Add(0); // Compression method
            
            bytes.AddRange(langTagBytes);
            bytes.Add(0); // Null terminator
            
            bytes.AddRange(transKeyBytes);
            bytes.Add(0); // Null terminator

            if (state.EncoderSettings.TextCompression)
            {
                bytes.AddRange(compressedData);
            }
            else /*not compressed*/
            {
                bytes.AddRange(textBytes);
            }

            return bytes.ToArray();
        }

        internal static iTXt FromTextItem(ITextItem item)
        {
            var itxt = new iTXt();
            itxt.Key = item.Key;
            itxt.LangTag = item.LangTag;
            itxt.TransKey = item.TransKey;
            itxt.Text = item.Text;

            return itxt;
        }
    }
}
