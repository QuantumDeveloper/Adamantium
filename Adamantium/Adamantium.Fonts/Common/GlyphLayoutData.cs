using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.Common
{
    public class GlyphLayoutData
    {
        private List<Glyph> substitutions;

        public GlyphPosition Position { get; set; }

        public IReadOnlyCollection<Glyph> Substitutions => substitutions.AsReadOnly();

        public GlyphLayoutData(GlyphPosition position)
        {
            Position = position;
            substitutions = new List<Glyph>();
        }
        
        public GlyphLayoutData(params Glyph[] glyphs)
        {
            substitutions = new List<Glyph>(glyphs);
        }

        public void AppendGlyphs(params Glyph[] glyphs)
        {
            var tmpList = new List<Glyph>();
            foreach (var glyph in glyphs)
            {
                if (!substitutions.Contains(glyph))
                {
                    tmpList.Add(glyph);
                }
            }
            
            substitutions.AddRange(tmpList);
        }

        public void SubtractData(GlyphLayoutData data)
        {
            Position -= data.Position;
            substitutions = substitutions.Except(data.substitutions).ToList();
        }
    }
}
