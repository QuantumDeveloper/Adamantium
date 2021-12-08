using System;
using Adamantium.Fonts.Tables.WOFF;

namespace Adamantium.Fonts.Tables
{
    internal class FontCollectionEntry
    {
        /// <summary>
        /// The number of tables in this font
        /// </summary>
        public UInt16 NumTables { get; set; }
        
        /// <summary>
        /// The "sfnt version" of the font
        /// </summary>
        public UInt32 Flavor { get; set; }
        
        public FontType FontType { get; set; }
        
        public UInt16[] Index {get; set; }
        
        public Woff2Table[] Tables { get; set; }
    }
}