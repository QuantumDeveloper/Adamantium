using System;

namespace Adamantium.Fonts.Tables.WOFF
{
    internal class WoffHeader
    {
        /// <summary>
        /// 0x774F4646 'wOFF'
        /// </summary>
        public UInt32 Signature { get; set; }
        
        /// <summary>
        /// The "sfnt version" of the input font.
        /// </summary>
        public UInt32 Flavor { get; set; }
        
        /// <summary>
        /// Type of the internal font
        /// </summary>
        public InnerFontType InnerFontType { get; set; }
        
        /// <summary>
        /// Total size of the WOFF file.
        /// </summary>
        public UInt32 Length { get; set; }
        
        /// <summary>
        /// Number of entries in directory of font tables.
        /// </summary>
        public UInt16 NumTables { get; set; }
        
        /// <summary>
        /// Reserved; set to zero.
        /// </summary>
        public UInt16 Reserved { get; set; }
        
        /// <summary>
        /// Total size needed for the uncompressed font data, including the sfnt header, directory, and font tables (including padding).
        /// </summary>
        public UInt32 TotalSfntSize { get; set; }
        
        /// <summary>
        /// Major version of the WOFF file.
        /// </summary>
        public UInt16 MajorVersion { get; set; }
        
        /// <summary>
        /// Minor version of the WOFF file.
        /// </summary>
        public UInt16 MinorVersion { get; set; }
        
        /// <summary>
        /// Offset to metadata block, from beginning of WOFF file.
        /// </summary>
        public UInt32 MetaOffset { get; set; }
        
        /// <summary>
        /// Length of compressed metadata block.
        /// </summary>
        public UInt32 MetaLength { get; set; }
        
        /// <summary>
        /// Uncompressed size of metadata block.
        /// </summary>
        public UInt32 MetaOrigLength { get; set; }
        
        /// <summary>
        /// Offset to private data block, from beginning of WOFF file.
        /// </summary>
        public UInt32 PrivOffset { get; set; }
        
        /// <summary>
        /// Length of private data block.
        /// </summary>
        public UInt32 PrivLength { get; set; }
    }
}