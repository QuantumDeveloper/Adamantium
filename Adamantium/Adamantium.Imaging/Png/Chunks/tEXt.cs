using System.Collections.Generic;
using System.Text;

namespace Adamantium.Imaging.Png.Chunks
{
    internal class tEXt : Chunk
    {
        public string Key { get; set; }

        public string Text { get; set; }

        internal override byte[] GetChunkBytes(PngState state)
        {
            var bytes = new List<byte>();

            bytes.AddRange(GetNameAsBytes());
            var keyWord = Encoding.ASCII.GetBytes(Key);
            if (keyWord.Length < 1 || keyWord.Length > 79)
            {
                throw new PngEncoderException(89);
            }

            bytes.AddRange(keyWord);
            bytes.Add(0); // Null terminator;
            var textString = Encoding.ASCII.GetBytes(Text);
            bytes.AddRange(textString);

            return bytes.ToArray();
        }

        internal static tEXt FromTextItem(TXTItem item)
        {
            var text = new tEXt();
            text.Key = item.Key;
            text.Text = item.Text;

            return text;
        }
    }
}
