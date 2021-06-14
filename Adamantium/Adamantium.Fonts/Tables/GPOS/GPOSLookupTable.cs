using System;
using Adamantium.Fonts.Tables.Layout;

namespace Adamantium.Fonts.Tables.GPOS
{
    internal class GPOSLookupTable
    {
        /// <summary>
        /// Different enumerations for GSUB and GPOS
        /// </summary>
        public GPOSLookupType LookupType { get; set; }
        
        /// <summary>
        /// Lookup qualifiers
        /// </summary>
        public LookupFlags LookupFlag { get; set; }
        
        /// <summary>
        /// Array of offsets to lookup subtables, from beginning of Lookup table
        /// </summary>
        public GPOSLookupSubTable[] SubTables { get; set; }
        
        /// <summary>
        /// Index (base 0) into GDEF mark glyph sets structure. This field is only present if the USE_MARK_FILTERING_SET lookup flag is set.
        /// </summary>
        public UInt16 MarkFilteringSet { get; set; }
    }
}