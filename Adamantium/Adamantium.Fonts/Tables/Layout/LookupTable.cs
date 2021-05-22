using System;

namespace Adamantium.Fonts.Tables.Layout
{
    internal class LookupTable
    {
        /// <summary>
        /// Different enumerations for GSUB and GPOS
        /// </summary>
        public UInt16 LookupType { get; set; }
        
        /// <summary>
        /// Lookup qualifiers
        /// </summary>
        public LookupFlags LookupFlag { get; set; }
        
        /// <summary>
        /// Array of offsets to lookup subtables, from beginning of Lookup table
        /// </summary>
        public LookupSubtable[] Subtables { get; set; }
        
        /// <summary>
        /// Index (base 0) into GDEF mark glyph sets structure. This field is only present if the USE_MARK_FILTERING_SET lookup flag is set.
        /// </summary>
        public UInt16 MarkFilteringSet { get; set; }
    }
}