using System;
using System.IO;
using System.Text;

namespace Adamantium.Fonts
{
    internal class FontTypeReader : BinaryReader
    {
        private string fontPath;
        public FontTypeReader(String path) : this(File.Open(path, FileMode.Open, FileAccess.Read))
        {
            fontPath = path;
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

        public FontType GetFontType()
        {
            if (IsOTF())
            {
                return FontType.OTF;
            }
            else if (IsWOFF())
            {
                return FontType.WOFF;
            }
            else if (IsWOFF2())
            {
                return FontType.WOFF2;
            }
            else if (Path.GetExtension(fontPath)?.ToLower() == ".ttf")
            {
                return FontType.TTF;
            }
            
            return FontType.Unknown;
        }
        
        private bool IsOTF()
        {
            BaseStream.Position = 0;
            var bytes = new byte[4];
            BaseStream.Read(bytes, 0, 4);
            var header = Encoding.UTF8.GetString(bytes);
            return header == "ttcf" || header == "OTTO";
        }

        private bool IsWOFF()
        {
            BaseStream.Position = 0;
            var bytes = new byte[4];
            BaseStream.Read(bytes, 0, 4);
            var header = Encoding.UTF8.GetString(bytes);
            return header == "wOFF";
        }

        private bool IsWOFF2()
        {
            BaseStream.Position = 0;
            var bytes = new byte[4];
            BaseStream.Read(bytes, 0, 4);
            var header = Encoding.UTF8.GetString(bytes);
            return header == "wOF2";
        }
    }
}