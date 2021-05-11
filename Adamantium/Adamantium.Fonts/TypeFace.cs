using System;
using System.Collections.Generic;
using System.Globalization;
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
        

        private Dictionary<UInt32, Glyph> unicodeToGlyph;
        private Dictionary<string, Glyph> nameToGlyph;

        public TypeFace()
        {
            fonts = new List<IFont>();
            glyphs = new List<Glyph>();
            unicodes = new List<uint>();
            unicodeToGlyph = new Dictionary<uint, Glyph>();
            
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
        
        public IReadOnlyCollection<uint> Unicodes => unicodes.AsReadOnly();
        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
        
        public IReadOnlyCollection<string> ErrorMessages => errorMessages.AsReadOnly();
        
        public Glyph GetGlyphByName(string name)
        {
            if (!nameToGlyph.TryGetValue(name, out var glyph))
            {
                return null;
            }

            return glyph;
        }
        
        public Glyph GetGlyphByCharacter(char character)
        {
            return GetGlyphByUnicode(character);
        }

        public Glyph GetGlyphByUnicode(uint unicode)
        {
            if (!unicodeToGlyph.TryGetValue(unicode, out var glyph))
            {
                return null;
            }

            return glyph;
        }

        public Glyph GetGlyphByIndex(uint index)
        {
            return glyphs[(int)index];
        }
        
        internal void SetGlyphs(IEnumerable<Glyph> glyphsArray)
        {
            glyphs.Clear();
            glyphs.AddRange(glyphsArray);
        }

        internal void UpdateGlyphNamesCache()
        {
            nameToGlyph = glyphs.ToDictionary(x => x.Name);
        }

        internal void SetGlyphUnicodes(Dictionary<uint, List<uint>> glyphMapping)
        {
            unicodes.Clear();
            unicodeToGlyph.Clear();
            
            foreach (var (key, value) in glyphMapping)
            {
                unicodes.AddRange(value);
                foreach (var unicode in value)
                {
                    unicodeToGlyph[unicode] = GetGlyphByIndex(key);
                }
            }
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
            TypeFace typeFace = null;
            
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