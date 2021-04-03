using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.OTF
{
    internal class Font : IFont
    {
        private List<Glyph> glyphs;
        private List<UInt32> unicodes;

        public Font()
        {
            glyphs = new List<Glyph>();
            unicodes = new List<uint>();
        }
        
        public string Name { get; internal set; }
        public IReadOnlyCollection<uint> Unicodes => unicodes.AsReadOnly();
        public IReadOnlyCollection<Glyph> Glyphs => glyphs.AsReadOnly();
        
        public Glyph GetGlyphByName(string name)
        {
            throw new NotImplementedException();
        }

        public Glyph GetGlyphByUnicode(uint unicode)
        {
            throw new NotImplementedException();
        }

        public Glyph GetGlyphByIndex(uint index)
        {
            throw new NotImplementedException();
        }
        
        internal void SetGlyphs(IEnumerable<Glyph> glyphs)
        {
            this.glyphs.Clear();
            this.glyphs.AddRange(glyphs);
        }
    }
}