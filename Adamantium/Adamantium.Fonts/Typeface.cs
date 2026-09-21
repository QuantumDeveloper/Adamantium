using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Parsers;

namespace Adamantium.Fonts
{
    public class Typeface
    {
        private readonly List<IFont> fonts;
        private List<Glyph> glyphs;
        private List<UInt32> unicodes;
        private readonly List<string> errorMessages;
        internal IFontParser Parser { get; set; }

        public IFont CurrentFont { get; private set; }

        public Typeface()
        {
            fonts = new List<IFont>();
            glyphs = new List<Glyph>();
            unicodes = new List<uint>();

            errorMessages = new List<string>();
        }

        public IReadOnlyList<IFont> Fonts => fonts.AsReadOnly();

        public uint GlyphCount => (uint)glyphs.Count;
        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
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

        public static Typeface LoadSystemFont(string fontName, byte sampleResolution = 3)
        {
            string fontsFolder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            var files = Directory.GetFiles(fontsFolder).Where(x=>!x.ToLower().EndsWith("fon")).ToArray();
            string fontFile = string.Empty;
            foreach (var file in files)
            {
                var typeFace = GetFontName(file);
                if (typeFace != null && typeFace.GetFont(0).FullName.ToLower() == fontName.ToLower())
                {
                    fontFile = file;
                    break;
                }
            }

            if (string.IsNullOrEmpty(fontFile)) return null;

            return LoadFont(Path.Combine(fontsFolder, fontFile), sampleResolution);
        }
        
        public static Typeface GetFontName(string path)
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
                    parser = new TTFParser(path, 0);
                    break;
                case FontType.Otf:
                    parser = new OTFParser(path, 0);
                    break;
                case FontType.Woff:
                    parser = new WoffParser(path, 0);
                    break;
                case FontType.Woff2:
                    parser = new Woff2Parser(path, 0);
                    break;
                default:
                    return null;
                    break;
            }

            parser.ReadFontName();

            return parser.Typeface;
        }

        public static Typeface LoadFont(string path, byte sampleResolution = 3)
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

            return parser.Typeface;
        }

        public static async Task<Typeface> LoadFontAsync(string path, byte sampleResolution)
        {
            return await Task.Run(()=> LoadFont(path, sampleResolution));
        }

        public static Typeface LoadFont(byte[] fontData, byte sampleResolution)
        {
            var fontStream = new FontStreamReader(fontData);
            return LoadFont(fontStream, sampleResolution);
        }

        public static async Task<Typeface> LoadFontAsync(byte[] fontData, byte sampleResolution)
        {
            return await Task.Run(() => LoadFont(fontData, sampleResolution));
        }

        public static Typeface LoadFont(FontStreamReader fontStream, byte sampleResolution)
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

            return parser.Typeface;
        }

        public static async Task<Typeface> LoadFontAsync(FontStreamReader fontStream, byte sampleResolution)
        {
            return await Task.Run(() => LoadFont(fontStream, sampleResolution));
        }
    }
}