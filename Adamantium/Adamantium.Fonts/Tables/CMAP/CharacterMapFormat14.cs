using System;
using System.Collections.Generic;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Tables.CMAP
{
    /// <summary>
    /// Many-to-one range mappings
    /// This subtable provides for situations in which the same glyph is used for
    /// hundreds or even thousands of consecutive characters spanning across multiple ranges of the code space.
    /// This subtable format may be useful for “last resort” fonts, although these fonts may use other suitable
    /// subtable formats as well. (For “last-resort” fonts, see also the 'head' table flags, bit 14.)

    /// Unicode Variation Sequences
    /// Subtable format 14 specifies the Unicode Variation Sequences (UVSes) supported by the font.
    /// A Variation Sequence, according to the Unicode Standard, comprises a base character
    /// followed by a variation selector. For example, <U+82A6, U+E0101>.
    /// This subtable format must only be used under platform ID 0 and encoding ID 5.
    /// </summary>
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