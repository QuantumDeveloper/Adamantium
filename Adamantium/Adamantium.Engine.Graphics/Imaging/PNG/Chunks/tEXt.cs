using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Adamantium.Engine.Graphics.Imaging.PNG.Chunks
{
    internal class tEXt : Chunk
    {
        public string Key { get; set; }

        public string Text { get; set; }

        internal override byte[] GetChunkBytes(PNGState state)
        {
            var bytes = new List<byte>();
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(Key.Length + 1 + Text.Length));
            bytes.AddRange(GetNameAsBytes());
            var keyWord = Encoding.ASCII.GetBytes(Key);
            if (keyWord.Length < 1 || keyWord.Length > 79)
            {
                throw new PNGEncoderException("Keyword should have from 1 to 79 chars");
            }

            bytes.AddRange(keyWord);
            bytes.Add(0); // Null terminator;
            var textString = Encoding.ASCII.GetBytes(Text);
            bytes.AddRange(textString);

            var crc = CRC32.CalculateCheckSum(bytes.ToArray());
            bytes.AddRange(Utilities.GetBytesWithReversedEndian(crc));

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
