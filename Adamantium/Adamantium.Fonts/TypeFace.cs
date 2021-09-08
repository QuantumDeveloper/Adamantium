using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Fonts.Parsers;

namespace Adamantium.Fonts
{
    public class TypeFace
    {
        private readonly List<IFont> fonts;
        private List<Glyph> glyphs;
        private List<UInt32> unicodes;
        private readonly List<string> errorMessages;
        
        public IFont CurrentFont { get; private set; }

        public TypeFace()
        {
            fonts = new List<IFont>();
            glyphs = new List<Glyph>();
            unicodes = new List<uint>();

            errorMessages = new List<string>();
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

        public IFont GetFont(string fullName)
        {
            return fonts.FirstOrDefault(x => x.FullName == fullName);
        }
        
        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
        
        public IReadOnlyCollection<string> ErrorMessages => errorMessages.AsReadOnly();

        internal void UpdateGlyphNames()
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

        public static TypeFace LoadFont(string path, byte sampleResolution)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(nameof(path));

            var reader = new FontTypeReader(path);
            var fontType = reader.GetFontType();
            reader.Close();
            TypeFace typeFace;
            
            switch (fontType)
            {
                case FontType.TTF:
                    typeFace = TTFParser.Parse(path, sampleResolution);
                    break;
                case FontType.OTF:
                    typeFace = OTFParser.Parse(path, sampleResolution);
                    break;
                case FontType.WOFF:
                    typeFace = WoffParser.Parse(path, sampleResolution);
                    break;
                case FontType.WOFF2:
                    typeFace = Woff2Parser.Parse(path, sampleResolution);
                    break;
                default:
                    throw new NotSupportedException("This font type is not supported");
            }

            return typeFace;
        }

        public static async Task<TypeFace> LoadFontAsync(string path, byte sampleResolution)
        {
            return await Task.Run(()=> LoadFont(path, sampleResolution));
        }
    }
}