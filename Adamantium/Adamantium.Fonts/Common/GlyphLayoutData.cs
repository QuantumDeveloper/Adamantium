using MessagePack;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.Common
{
    [MessagePackObject]
    public class GlyphLayoutData
    {
        [Key(0)]
        private List<uint> substitutions;

        [Key(1)]
        public GlyphPosition Position { get; set; }

        [IgnoreMember]
        public IReadOnlyCollection<uint> Substitutions => substitutions.AsReadOnly();

        public GlyphLayoutData()
        {
            substitutions = new List<uint>();
        }

        public GlyphLayoutData(GlyphPosition position) : this()
        {
            Position = position;
        }
        
        public GlyphLayoutData(params uint[] glyphIndices)
        {
            substitutions = new List<uint>(glyphIndices);
        }
        
        public GlyphLayoutData(params ushort[] glyphIndices) : this()
        {
            for (int i = 0; i < glyphIndices.Length; i++)
            {
                substitutions.Add(glyphIndices[i]);
            }
        }

        public void AppendGlyphs(params uint[] glyphs)
        {
            var tmpList = new List<uint>();
            foreach (var glyph in glyphs)
            {
                if (!substitutions.Contains(glyph))
                {
                    tmpList.Add(glyph);
                }
            }
            
            substitutions.AddRange(tmpList);
        }
        
        public void AppendGlyphs(params ushort[] glyphs)
        {
            var tmpList = new List<uint>();
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
