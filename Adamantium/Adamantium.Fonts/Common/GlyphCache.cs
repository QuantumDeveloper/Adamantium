using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Adamantium.Fonts.Common
{
    internal class GlyphCache
    {
        private Dictionary<Glyph, GlyphFeatureData> glyphCache;

        public GlyphCache(Glyph glyph, GlyphFeatureData data)
        {
            glyphCache = new Dictionary<Glyph, GlyphFeatureData>();

            glyphCache[glyph] = data;
        }

        public void AddToGlyphCache(Glyph glyph, GlyphPosition position)
        {
            glyphCache[glyph].Position += position;
        }

        /*public void AddToGlyphCache(Glyph glyph, GlyphPosition position)
        {
            glyphCache[glyph].Position += position;
        }*/

        public bool IsGlyphCached(Glyph glyph)
        {
            return glyphCache.ContainsKey(glyph);
        }

        public void Clear()
        {
            glyphCache.Clear();
        }
    }
}
