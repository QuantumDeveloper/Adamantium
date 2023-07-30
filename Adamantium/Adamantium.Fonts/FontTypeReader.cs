using System;
using System.IO;
using System.Text;

namespace Adamantium.Fonts
{
    public class FontTypeReader : BinaryReader
    {
        private string fontPath;
        public FontTypeReader(String path) : this(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
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
                return FontType.Otf;
            }
            else if (IsWOFF())
            {
                return FontType.Woff;
            }
            else if (IsWOFF2())
            {
                return FontType.Woff2;
            }
            else if (Path.GetExtension(fontPath)?.ToLower() == ".ttf")
            {
                return FontType.Ttf;
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