using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables
{
    internal class KerningSubtable
    {
        public UInt16 Version { get; internal set; }
        public UInt16 Length { get; internal set; }

        public bool Horizontal { get; internal set; } // true - horizontal kerning, false - vertical
        public bool Minimum { get; internal set; }
        
        /// <summary>
        /// Cross-stream:                                                        
        /// If text is normally written horizontally, kerning will be vertical.  
        /// If kerning values are positive, the text will be kerned up.          
        /// If they are negative, the text will be kerned down.                  
        /// If text is normally written vertically, kerning will be horizontal.  
        /// If kerning values are positive, the text will be kerned to the right.
        /// If they are negative, the text will be kerned to the left.           
        /// </summary>
        public bool CrossStream { get; internal set; }
        public bool Override { get; internal set; }
        public Byte Format { get; internal set; } // set the format of this subtable (0-3 currently defined)
        public UInt16 NumberOfPairs { get; internal set; }
        public UInt16 SearchRange { get; internal set; }
        public UInt16 EntrySelector { get; internal set; }
        public UInt16 RangeShift { get; internal set; }
        public Dictionary<UInt32, Int16> KerningValues { get; } // key is  "left | right" glyph indexes

        public KerningSubtable()
        {
            KerningValues = new Dictionary<uint, short>();
        }
    };
}
