using System;
using System.Collections.Generic;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Tables.CMAP
{
    /// <summary>Format 14: the Unicode Variation Sequences (a base character plus a variation selector) the font supports;
    /// used only under platform 0, encoding 5.</summary>
    public class CharacterMapFormat14 : CharacterMap
    {
        public CharacterMapFormat14()
        {
            VarSelectors = new Dictionary<uint, VariationSelector>();
        }
        
        public override UInt16 Format => 14;
        
        public UInt32 Length { get; set; }
        
        public UInt32 NumVarSelectorRecords { get; set; }
        
        public Dictionary<uint, VariationSelector> VarSelectors { get; }

        public override uint GetGlyphIndex(uint character) => 0;

        public uint CharacterPairToGlyphIndex(uint character, ushort defaultGlyphIndex, uint nextCharacter)
        {
            if (VarSelectors.TryGetValue(nextCharacter, out var selector))
            {
                if (selector.UVSMappings.TryGetValue(character, out var glyphIndex))
                {
                    return glyphIndex;
                }

                // If the sequence is a default UVS, return the default glyph
                for (int i = 0; i < selector.DefaultStartCodes.Count; ++i)
                {
                    if (character >= selector.DefaultStartCodes[i] && character < selector.DefaultStartCodes[i])
                    {
                        return defaultGlyphIndex;
                    }
                }

                return defaultGlyphIndex;
            }

            return 0;
        }

        public override void CollectUnicodeChars(List<uint> unicodes)
        {
            foreach (var selector in VarSelectors)
            {
                
            }
        }

        public override void GetUnicodeToGlyphMappings(Dictionary<uint, uint> unicodeToGlyph)
        {
            foreach (var selector in VarSelectors)
            {
                foreach (var uvsMapping in selector.Value.UVSMappings)
                {
                    unicodeToGlyph[uvsMapping.Key] = uvsMapping.Value;
                }
            }
        }
    }
}