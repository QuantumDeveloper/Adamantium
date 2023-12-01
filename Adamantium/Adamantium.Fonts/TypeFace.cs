using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Parsers;
using MessagePack;

namespace Adamantium.Fonts
{
    [MessagePackObject]
    public class TypeFace
    {
        [Key(0)]
        private readonly List<IFont> fonts;
        [Key(1)]
        private List<Glyph> glyphs;
        [Key(2)]
        private List<UInt32> unicodes;
        [Key(3)]
        private readonly List<string> errorMessages;
        [IgnoreMember]
        internal IFontParser Parser { get; set; }

        [IgnoreMember]
        public IFont CurrentFont { get; private set; }

        public TypeFace()
        {
            fonts = new List<IFont>();
            glyphs = new List<Glyph>();
            unicodes = new List<uint>();

            errorMessages = new List<string>();
        }

        [IgnoreMember]
        public IReadOnlyCollection<IFont> Fonts => fonts.AsReadOnly();

        [IgnoreMember]
        public uint GlyphCount => (uint)glyphs.Count;
        [IgnoreMember]
        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
        [IgnoreMember]
        public IReadOnlyCollection<string> ErrorMessages => errorMessages.AsReadOnly();

        internal void AddFont(IFont font)
        {
            fonts.Add(font);
        }

        public IFont GetFont(uint index)
        {
            return fonts[(int)index];
        }

        public IFont GetFont(string fullName)
        {
            return fonts.FirstOrDefault(x => x.FullName == fullName);
        }

        public void UpdateGlyphNames()
        {
            foreach (var font in fonts)
            {
                font.UpdateGlyphNamesCache();
            }
        }

        internal void SetDefaultFont()
        {
            CurrentFont = fonts[0];
        }

        internal void SetCurrentFont(IFont font)
        {
            if (!fonts.Contains(font)) return;

            CurrentFont = font;
        }
        
        public bool GetGlyphByIndex(uint index, out Glyph glyph)
        {
            glyph = null;
            
            if (index >= glyphs.Count) return false;
            
            glyph = glyphs[(int)index];

            return true;
        }
        
        internal void SetGlyphs(IEnumerable<Glyph> glyphsArray)
        {
            glyphs.Clear();
            glyphs.AddRange(glyphsArray);
        }

        internal void AddErrorMessage(string message)
        {
            errorMessages.Add(message);
        }

        public byte[] GetFontAsBytesArray()
        {
            return Parser.GetFontBytes();
        }

        public static TypeFace LoadSystemFont(string fontName, byte sampleResolution)
        {
            string fontsfolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var files = Directory.GetFiles(fontsfolder);
            var fontFile = files.FirstOrDefault(x => string.Equals(Path.GetFileNameWithoutExtension(x), fontName, StringComparison.CurrentCultureIgnoreCase));

            if (fontFile == null) return null;

            return LoadFont(Path.Combine(fontsfolder, fontFile), sampleResolution);
        }

        public static TypeFace LoadFont(string path, byte sampleResolution)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(nameof(path));

            var reader = new FontTypeReader(path);
            var fontType = reader.GetFontType();
            reader.Close();
            IFontParser parser = null; 
            
            switch (fontType)
            {
                case FontType.Ttf:
                    parser = new TTFParser(path, sampleResolution);
                    break;
                case FontType.Otf:
                    parser = new OTFParser(path, sampleResolution);
                    break;
                case FontType.Woff:
                    parser = new WoffParser(path, sampleResolution);
                    break;
                case FontType.Woff2:
                    parser = new Woff2Parser(path, sampleResolution);
                    break;
                default:
                    throw new NotSupportedException("This font type is not supported");
            }

            parser.Parse();

            return parser.TypeFace;
        }

        public static async Task<TypeFace> LoadFontAsync(string path, byte sampleResolution)
        {
            return await Task.Run(()=> LoadFont(path, sampleResolution));
        }

        public static TypeFace LoadFont(byte[] fontData, byte sampleResolution)
        {
            var fontStream = new FontStreamReader(fontData);
            return LoadFont(fontStream, sampleResolution);
        }

        public static async Task<TypeFace> LoadFontAsync(byte[] fontData, byte sampleResolution)
        {
            return await Task.Run(() => LoadFont(fontData, sampleResolution));
        }

        public static TypeFace LoadFont(FontStreamReader fontStream, byte sampleResolution)
        {
            var reader = new FontTypeReader(fontStream);
            var fontType = reader.GetFontType();
            reader.Close();
            fontStream.Position = 0;
            IFontParser parser = null;

            switch (fontType)
            {
                case FontType.Ttf:
                    parser = new TTFParser(fontStream, sampleResolution);
                    break;
                case FontType.Otf:
                    parser = new OTFParser(fontStream, sampleResolution);
                    break;
                case FontType.Woff:
                    parser = new WoffParser(fontStream, sampleResolution);
                    break;
                case FontType.Woff2:
                    parser = new Woff2Parser(fontStream, sampleResolution);
                    break;
                default:
                    throw new NotSupportedException("This font type is not supported");
            }

            parser.Parse();

            return parser.TypeFace;
        }

        public static async Task<TypeFace> LoadFontAsync(FontStreamReader fontStream, byte sampleResolution)
        {
            return await Task.Run(() => LoadFont(fontStream, sampleResolution));
        }
    }
}