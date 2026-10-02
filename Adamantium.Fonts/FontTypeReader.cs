using System;
using System.IO;
using System.Text;

namespace Adamantium.Fonts
{
    public class FontTypeReader : BinaryReader
    {
        public FontTypeReader(String path) : this(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
        }

        public FontTypeReader(Stream input) : base(input)
        {
        }

        public FontTypeReader(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        public FontTypeReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
        {
        }

        /// <summary>The format of the font, told by the signature it starts with.</summary>
        public FontType GetFontType()
        {
            BaseStream.Position = 0;
            var bytes = new byte[4];
            if (BaseStream.Read(bytes, 0, 4) < 4)
            {
                return FontType.Unknown;
            }

            switch (Encoding.ASCII.GetString(bytes))
            {
                case "OTTO":
                case "ttcf":
                    return FontType.Otf;
                case "wOFF":
                    return FontType.Woff;
                case "wOF2":
                    return FontType.Woff2;
                case "true":
                    return FontType.Ttf;
            }

            return bytes[0] == 0 && bytes[1] == 1 && bytes[2] == 0 && bytes[3] == 0 ? FontType.Ttf : FontType.Unknown;
        }
    }
}
