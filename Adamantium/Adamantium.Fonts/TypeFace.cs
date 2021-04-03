using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Adamantium.Fonts.OTF;
using Adamantium.Fonts.TTF;

namespace Adamantium.Fonts
{
    public class TypeFace
    {
        private List<IFont> fonts;

        public TypeFace()
        {
            fonts = new List<IFont>();
        }

        public IReadOnlyCollection<IFont> Fonts => fonts.AsReadOnly();

        internal void AddFont(IFont font)
        {
            fonts.Add(font);
        }

        public IFont GetFont(uint index)
        {
            return fonts[(int)index];
        }
        
        public static TypeFace LoadFont(string path, uint sampleResolution)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(nameof(path));

            var reader = new FontHeaderReader(path);
            var fontType = reader.GetFontType();
            reader.Close();
            IFontParser fontParser = null;

            switch (fontType)
            {
                case FontType.TTF:
                    fontParser = new TTFParser(path, sampleResolution);
                    break;
                case FontType.OTF:
                    fontParser = new OTFParser(path, sampleResolution);
                    break;
                case FontType.WOFF:
                    throw new NotSupportedException("WOFF fonts is not currently supported");
                case FontType.WOFF2:
                    throw new NotSupportedException("WOFF2 fonts is not currently supported"); 
                default:
                    throw new NotSupportedException("This font type is not supported");
            }

            return fontParser.TypeFace;
        }
        
        
    }

    internal class FontHeaderReader : BinaryReader
    {
        private string fontPath;
        public FontHeaderReader(String path) : this(File.Open(path, FileMode.Open, FileAccess.Read))
        {
            fontPath = path;
        }
    
        public FontHeaderReader(Stream input) : base(input)
        {
        }

        public FontHeaderReader(Stream input, Encoding encoding) : base(input, encoding)
        {
        }

        public FontHeaderReader(Stream input, Encoding encoding, bool leaveOpen) : base(input, encoding, leaveOpen)
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
            else if (Path.GetExtension(fontPath).ToLower() == "ttf")
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
            return header == "WOFF";
        }

        private bool IsWOFF2()
        {
            BaseStream.Position = 0;
            var bytes = new byte[4];
            BaseStream.Read(bytes, 0, 4);
            var header = Encoding.UTF8.GetString(bytes);
            return header == "WOF2";
        }
    }
    
}