using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GSUB
{
    internal class GlyphSubstitutionTable
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
        
        public GSUBLookupTable[] LookupList { get; set; }
    }
}