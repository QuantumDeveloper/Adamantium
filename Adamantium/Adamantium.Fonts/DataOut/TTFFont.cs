using System;
using System.Collections.Generic;
using Adamantium.Fonts.TTF;

namespace Adamantium.Fonts.DataOut
{
    public class TTFFont
    {
        public string FontFullName { get; internal set; }
        public ushort UnitsPerEm { get; internal set; } 
        public ushort LowestRecPPEM { get; internal set; } // smallest readable size in pixels
        public Int32 LineSpace { get; internal set; } // space between base line and base line
        public Dictionary<UInt16, UInt16> CharToGlyphIndexMapWindowsUnicode { get; internal set; } // char to glyph index mapping (for Windows Unicode)
        public TTFKerningSubtable KerningData { get; internal set; }
        public bool HasKerningData => KerningData != null;

        public GlyphData[] GlyphData { get; internal set; }

        public List<string> ErrorMessages { get; }

        internal TTFFont()
        {
            CharToGlyphIndexMapWindowsUnicode = new Dictionary<ushort, ushort>();
            ErrorMessages = new List<string>();
        }

        public GlyphData GetGlyphForCharacter(UInt16 character)
        {
            ushort index = 0;

            if (CharToGlyphIndexMapWindowsUnicode.ContainsKey(character))
            {
                index = CharToGlyphIndexMapWindowsUnicode[character];
            }

            return GlyphData[index];
        }

        public Int16 GetKerningValue(UInt16 leftCharacter, UInt16 rightCharacter)
        {
            if (KerningData == null)
            {
                return 0;
            }

            UInt16 leftIndex = 0;
            UInt16 rightIndex = 0;

            if (CharToGlyphIndexMapWindowsUnicode.ContainsKey(leftCharacter))
            {
                leftIndex = CharToGlyphIndexMapWindowsUnicode[leftCharacter];
            }

            if (CharToGlyphIndexMapWindowsUnicode.ContainsKey(rightCharacter))
            {
                rightIndex = CharToGlyphIndexMapWindowsUnicode[rightCharacter];
            }

            Int16 kerningValue = 0;
            
            UInt32 key = TTFParser.GenerateKerningKey(leftIndex, rightIndex);

            if (KerningData.KerningValues.ContainsKey(key))
            {
                kerningValue = KerningData.KerningValues[key];
            }

            return kerningValue;
        }
    }
}
