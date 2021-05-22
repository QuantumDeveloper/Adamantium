using System;

namespace Adamantium.Fonts.Tables.Layout
{
    public class GlyphPositioningTable
    {
        public UInt16 MajorVersion { get; set; }
        
        public UInt16 MinorVersion { get; set; }
        
        /// <summary>
        /// Offset to ScriptList table, from beginning of GPOS table
        /// </summary>
        public UInt16 ScriptListOffset { get; set; }
        
        /// <summary>
        /// Offset to FeatureList table, from beginning of GPOS table
        /// </summary>
        public UInt16 FeatureListOffset { get; set; }
        
        /// <summary>
        /// Offset to LookupList table, from beginning of GPOS table
        /// </summary>
        public UInt16 LookupListOffset { get; set; }
        
        /// <summary>
        /// Offset to FeatureVariations table, from beginning of GPOS table (may be NULL)
        /// </summary>
        public UInt32 FeatureVariationsOffset { get; set; }
        
    }
}