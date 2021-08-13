using System;
using Adamantium.Fonts.Tables.GSUB;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class GlyphPositioningTable : IFontLayout
    {
        public UInt16 MajorVersion { get; set; }
        
        public UInt16 MinorVersion { get; set; }
        
        /// <summary>
        /// Offset to ScriptList table, from beginning of GPOS table
        /// </summary>
        public ScriptTable[] ScriptList { get; set; }
        
        /// <summary>
        /// Offset to FeatureList table, from beginning of GPOS table
        /// </summary>
        public FeatureTable[] FeatureList { get; set; }
        
        /// <summary>
        /// Offset to LookupList table, from beginning of GPOS table
        /// </summary>
        public ILookupTable[] LookupList { get; set; }
        
        /// <summary>
        /// Offset to FeatureVariations table, from beginning of GPOS table (may be NULL)
        /// </summary>
        public UInt32 FeatureVariationsOffset { get; set; }
        
    }
}