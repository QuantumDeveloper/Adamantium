using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables
{
    internal class CharacterToGlyphTable
    {
        private readonly Dictionary<UInt32, UInt32> unicodeToGlyph;
        internal readonly Dictionary<UInt32, List<UInt32>> GlyphToUnicode;
        private readonly Dictionary<UInt32, UInt32> characterToGlyph;
        
        public CharacterToGlyphTable()
        {
            unicodeToGlyph = new Dictionary<uint, uint>();
            characterToGlyph = new Dictionary<uint, uint>();
            GlyphToUnicode = new Dictionary<uint, List<uint>>();
        }
        
        public UInt16 Version { get; set; }
        
        public CharacterMap[] CharacterMaps { get; set; }

        public void CollectUnicodeToGlyphMappings()
        {
            foreach (var characterMap in CharacterMaps)
            {
                characterMap.GetUnicodeToGlyphMappings(unicodeToGlyph);
            }

            var hashSet = new HashSet<UInt32>();
            foreach (var kvp in unicodeToGlyph)
            {
                if (hashSet.Add(kvp.Value))
                {
                    GlyphToUnicode[kvp.Value] = new List<uint>();
                }

                GlyphToUnicode[kvp.Value].Add(kvp.Key);
            }
        }

        public UInt32 GetGlyphByCharacter(UInt32 character)
        {
            if (!characterToGlyph.TryGetValue(character, out var glyphIndex))
            {
                foreach (var map in CharacterMaps)
                {
                    var index = map.GetGlyphIndex(character);
                    if (index != 0)
                    {
                        characterToGlyph[character] = index;
                        break;
                    }
                }
            }

            return glyphIndex;
        }

        public UInt32[] GetUnicodesByGlyphId(UInt32 glyphId)
        {
            if (!GlyphToUnicode.TryGetValue(glyphId, out var unicodes))
            {
                return new uint[0];
            }

            return unicodes.ToArray();
        }
    }
}